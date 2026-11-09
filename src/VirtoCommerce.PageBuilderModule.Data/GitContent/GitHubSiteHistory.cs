using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>History of database pages and theme files in the repository the module created for the store: a version is a commit.</summary>
public class GitHubSiteHistory(
    IGitContentPolicy gitContentPolicy,
    IGitContentRepository gitContentRepository,
    IGitHubApiClient gitHubApiClient,
    IGitHubConnectionService gitHubConnectionService,
    IGroupedPageService groupedPageService,
    IThemeStorageFactory themeStorageFactory,
    IOptions<GitContentOptions> options,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<GitHubSiteHistory> logger) : IContentHistory
{
    public const string PagesFolder = "database-pages";
    public const string ThemeFolder = "theme";

    // the cursor goes to the client and back: protected, so only positions this history wrote reach GitHub
    private const string CursorProtectionPurpose = "VirtoCommerce.PageBuilderModule.ContentHistoryCursor";

    private readonly IDataProtector _cursorProtector = dataProtectionProvider.CreateProtector(CursorProtectionPurpose);

    // GitHub's limit on a GraphQL page
    private const int MaxPageSize = 100;

    // another save may move the branch between reading its head and moving it
    private const int MaxCommitAttempts = 3;

    private const int CommitShaBytes = 20;

    private const int PositionKeyBytes = 9;

    public virtual async Task<ContentHistoryStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default)
    {
        StoreRepository repository;
        try
        {
            repository = await gitContentPolicy.GetConfiguredRepositoryAsync(storeId, cancellationToken);
        }
        catch (GitContentException)
        {
            // a malformed repository setting is still a repository assigned by hand
            return ContentHistoryStatus.Unavailable;
        }

        if (repository == null)
        {
            return ContentHistoryStatus.SetupRequired;
        }

        if (!repository.IsPlatformManaged)
        {
            return ContentHistoryStatus.Unavailable;
        }

        return await gitContentPolicy.IsEnabledForStoreAsync(storeId, cancellationToken) ? ContentHistoryStatus.Enabled : ContentHistoryStatus.SetupRequired;
    }

    public virtual async Task RecordAsync(string storeId, ContentHistoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var files = GetCommitFiles(record);
        if (files.Count == 0)
        {
            return;
        }

        try
        {
            var repository = await GetHistoryRepositoryAsync(storeId, cancellationToken);
            if (repository == null)
            {
                return;
            }

            var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
            await CommitFilesAsync(repository, connection.BaseBranch, files, GetCommitMessage(record, files.Keys), CreateAuthor(record), cancellationToken);
        }
        // best effort: the save stands without its version
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The history of store {StoreId} missed a version: {Description}.", storeId, record.Description);
        }
    }

    public virtual async Task<ContentVersionPage> ListAsync(string storeId, IList<HistoryItem> items, string cursor, int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var continuation = ReadCursor(cursor);
        var positions = continuation.Positions;
        var result = AbstractTypeFactory<ContentVersionPage>.TryCreateInstance();
        result.Status = await GetStatusAsync(storeId, cancellationToken);
        if (result.Status != ContentHistoryStatus.Enabled)
        {
            return result;
        }

        var sources = items
            .Select(item => (Item: item, Path: GetFilePath(item)))
            .Where(x => x.Path != null)
            .DistinctBy(x => x.Path, StringComparer.Ordinal)
            .Select(x => new HistorySource(x.Item, x.Path, positions.TryGetValue(GetPositionKey(x.Path), out var position) ? position : null))
            .Where(x => !x.Position.Done)
            .ToList();
        if (sources.Count == 0)
        {
            return result;
        }

        take = Math.Clamp(take, 1, MaxPageSize);
        var repository = await gitContentPolicy.GetRepositoryAsync(storeId, cancellationToken);
        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
        var answer = await QueryHistoriesAsync(repository, connection.BaseBranch, continuation.Head, sources, take, cancellationToken);
        if (answer?.SelectToken("data.repository") is not JObject)
        {
            result.Status = ContentHistoryStatus.SetupRequired;
            return result;
        }

        var head = answer.SelectToken("data.repository.ref.target") ?? answer.SelectToken("data.repository.object");
        for (var i = 0; i < sources.Count; i++)
        {
            var history = head?[$"h{i}"];
            var source = sources[i];
            source.Nodes = (history?["nodes"] as JArray ?? []).Skip(source.Position.Skip).ToList();
            source.HasMore = history?.SelectToken("pageInfo.hasNextPage")?.Value<bool>() == true;
            source.EndCursor = history?.SelectToken("pageInfo.endCursor")?.Value<string>();
            source.LiveOid = source.Position.LiveFound ? null : ComputeBlobOid(await GetLiveContentAsync(storeId, source.Item, cancellationToken));
        }

        result.Versions = MergeVersions(sources, take);
        // later pages read the commit this page read, so a save made meanwhile does not shift them
        result.Cursor = WriteCursor(continuation.Head ?? head?["oid"]?.Value<string>(), positions, sources);

        return result;
    }

    public virtual async Task<string> ReadAsync(string storeId, HistoryItem item, string versionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var path = GetFilePath(item);
        var sha = GetCommitSha(versionId);
        if (path == null || sha == null)
        {
            return null;
        }

        var repository = await GetHistoryRepositoryAsync(storeId, cancellationToken);
        return repository == null ? null : await gitContentRepository.ReadFileAsync(repository, path, sha, cancellationToken);
    }

    /// <summary>Computes the git blob id of the content.</summary>
    public static string ComputeBlobOid(string content)
    {
        if (content == null)
        {
            return null;
        }

        var bytes = PageJson.Encoding.GetBytes(content);
        var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
#pragma warning disable CA5350 // git names blobs by SHA-1; this is an identity, not a security check
        return Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes]));
#pragma warning restore CA5350
    }

    /// <summary>Gets the store repository when it keeps the history.</summary>
    protected virtual async Task<string> GetHistoryRepositoryAsync(string storeId, CancellationToken cancellationToken)
    {
        return await GetStatusAsync(storeId, cancellationToken) == ContentHistoryStatus.Enabled
            ? await gitContentPolicy.GetRepositoryAsync(storeId, cancellationToken)
            : null;
    }

    /// <summary>Gets the file of the item in the repository; <c>null</c> for an item the history does not keep.</summary>
    protected virtual string GetFilePath(HistoryItem item) => item?.Type switch
    {
        HistoryItemType.Page when PageBuilderContentPaths.IsFolderName(item.Key) => $"{PagesFolder}/{item.Key}.json",
        HistoryItemType.Theme when PageBuilderContentPaths.IsHistoryThemeFile(item.Key) => $"{ThemeFolder}/{item.Key}",
        _ => null,
    };

    protected virtual IDictionary<string, string> GetCommitFiles(ContentHistoryRecord record)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in record.Entries.Where(x => x?.Content != null))
        {
            if (GetFilePath(entry.Item) is { } path)
            {
                files[path] = entry.Content;
            }
        }

        return files;
    }

    /// <summary>Commits the files to the branch as one commit. Returns its sha; <c>null</c> when no file changed.</summary>
    protected virtual async Task<string> CommitFilesAsync(string repository, string branch, IDictionary<string, string> files, string message,
        GitCommitAuthor author, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCommitAttempts; attempt++)
        {
            var head = await gitContentRepository.GetBranchHeadShaAsync(repository, branch, cancellationToken)
                ?? throw new GitContentException($"Branch \"{branch}\" of \"{repository}\" does not exist.");
            var baseTree = await gitHubApiClient.GetCommitTreeAsync(repository, head, cancellationToken);
            var tree = await gitHubApiClient.CreateTreeAsync(repository, baseTree, files, cancellationToken);
            if (tree == baseTree)
            {
                return null;
            }

            var commit = await gitHubApiClient.CreateCommitAsync(repository, tree, head, message, author, cancellationToken);
            if (await gitHubApiClient.FastForwardBranchAsync(repository, branch, commit, cancellationToken))
            {
                return commit;
            }
        }

        throw new GitContentException($"Branch \"{branch}\" of \"{repository}\" kept moving: the version was not recorded.");
    }

    protected virtual string GetCommitMessage(ContentHistoryRecord record, IEnumerable<string> files)
    {
        var message = $"{record.Description}\n\n{string.Join("\n", files.Order(StringComparer.Ordinal))}";

        return GitCommitMessage.WithTrailer(GitCommitMessage.WithVcUser(message, record.UserName), GitCommitMessage.VcSaveTrailer, record.SaveId);
    }

    protected virtual GitCommitAuthor CreateAuthor(ContentHistoryRecord record)
    {
        var author = AbstractTypeFactory<GitCommitAuthor>.TryCreateInstance();
        author.Name = string.IsNullOrEmpty(record.UserName) ? options.Value.FallbackAuthorName : record.UserName;
        author.Email = record.UserEmail ?? options.Value.FallbackAuthorEmail;
        return author;
    }

    /// <summary>Gets the content the site shows: the published page, the live theme file.</summary>
    protected virtual async Task<string> GetLiveContentAsync(string storeId, HistoryItem item, CancellationToken cancellationToken)
    {
        if (item.Type == HistoryItemType.Page)
        {
            var group = await groupedPageService.GetByIdAsync(item.Key);
            var page = group == null ? null : PageBuilderPageSelection.Order(group.Pages ?? []).FirstOrDefault(x => x.Status == Published);
            return page == null ? null : await groupedPageService.LoadContent(page.Id, cancellationToken);
        }

        return await themeStorageFactory.Open(storeId, PageBuilderContentPaths.GetThemeName(item.Key)).ReadAsync(PageBuilderContentPaths.GetPathInTheme(item.Key));
    }

    /// <summary>Reads the histories of the files in one request, from the head of the branch or from the commit a first page read.</summary>
    protected virtual Task<JObject> QueryHistoriesAsync(string repository, string branch, string head, IList<HistorySource> sources, int take,
        CancellationToken cancellationToken)
    {
        var (owner, name) = GitHubApiClient.ParseRepository(repository);
        var variables = new JObject
        {
            ["owner"] = owner,
            ["name"] = name,
        };
        var parameters = new StringBuilder("$owner:String!,$name:String!");
        if (head == null)
        {
            variables["branch"] = $"refs/heads/{branch}";
            parameters.Append(",$branch:String!");
        }
        else
        {
            variables["head"] = head;
            parameters.Append(",$head:GitObjectID!");
        }

        var histories = new StringBuilder();
        for (var i = 0; i < sources.Count; i++)
        {
            var position = sources[i].Position;
            variables[$"p{i}"] = sources[i].Path;
            variables[$"t{i}"] = Math.Min(position.Skip + take, MaxPageSize);
            variables[$"a{i}"] = position.After;
            parameters.Append($",$p{i}:String!,$t{i}:Int!,$a{i}:String");
            histories.Append($" h{i}: history(path:$p{i}, first:$t{i}, after:$a{i}){{ pageInfo{{ hasNextPage endCursor }} nodes{{ oid committedDate messageHeadline messageBody author{{ name email user{{ login }} }} file(path:$p{i}){{ oid }} }} }}");
        }

        var request = new JObject
        {
            ["query"] = head == null
                ? $"query({parameters}){{ repository(owner:$owner,name:$name){{ ref(qualifiedName:$branch){{ target{{ ... on Commit{{ oid{histories} }} }} }} }} }}"
                : $"query({parameters}){{ repository(owner:$owner,name:$name){{ object(oid:$head){{ ... on Commit{{ oid{histories} }} }} }} }}",
            ["variables"] = variables,
        };

        return gitHubApiClient.PostGraphQlAsync(options.Value.GraphQlUrl, owner, request, $"read the history of \"{repository}\"", cancellationToken);
    }

    /// <summary>
    /// Merges the files' histories into versions, newest first: a commit is one version listing the files it changed. Stops where a
    /// file has older changes not read yet.
    /// </summary>
    protected virtual IList<ContentVersion> MergeVersions(IList<HistorySource> sources, int take)
    {
        var versions = new List<ContentVersion>();
        var byOid = new Dictionary<string, ContentVersion>(StringComparer.Ordinal);
        while (true)
        {
            foreach (var source in sources)
            {
                while (source.Head is { } head && byOid.TryGetValue(GetOid(head), out var listed))
                {
                    AddItem(listed, source);
                }
            }

            if (versions.Count >= take || sources.Any(x => x.Head == null && x.HasMore))
            {
                break;
            }

            var newest = sources
                .Where(x => x.Head != null)
                .Select(x => x.Head)
                .OrderByDescending(x => GitHubApiClient.ReadDate(x["committedDate"]))
                .ThenBy(GetOid, StringComparer.Ordinal)
                .FirstOrDefault();
            if (newest == null)
            {
                break;
            }

            var version = CreateVersion(newest);
            versions.Add(version);
            byOid[GetOid(newest)] = version;
        }

        return versions;
    }

    protected virtual ContentVersion CreateVersion(JToken node)
    {
        var messageBody = node["messageBody"]?.Value<string>();

        var author = AbstractTypeFactory<ContentVersionAuthor>.TryCreateInstance();
        author.Name = GitCommitMessage.ReadTrailer(messageBody, GitCommitMessage.VcUserTrailer) ?? node.SelectToken("author.name")?.Value<string>();
        author.Email = node.SelectToken("author.email")?.Value<string>();
        author.Login = node.SelectToken("author.user.login")?.Value<string>();

        var version = AbstractTypeFactory<ContentVersion>.TryCreateInstance();
        version.VersionId = GetVersionId(GetOid(node));
        version.Date = GitHubApiClient.ReadDate(node["committedDate"]);
        version.Author = author;
        version.Description = node["messageHeadline"]?.Value<string>();
        version.SaveId = GitCommitMessage.ReadTrailer(messageBody, GitCommitMessage.VcSaveTrailer);
        version.Published = true;

        return version;
    }

    protected virtual string GetVersionId(string sha) => Base64Url.EncodeToString(Convert.FromHexString(sha));

    /// <summary>Gets the commit a version id names; <c>null</c> for anything but a version id.</summary>
    protected virtual string GetCommitSha(string versionId)
    {
        if (string.IsNullOrEmpty(versionId) || versionId.Length != Base64Url.GetEncodedLength(CommitShaBytes))
        {
            return null;
        }

        try
        {
            var bytes = Base64Url.DecodeFromChars(versionId);
            return bytes.Length == CommitShaBytes ? Convert.ToHexStringLower(bytes) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <exception cref="ArgumentException">The cursor is not one this history wrote.</exception>
    protected virtual HistoryCursor ReadCursor(string cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return new HistoryCursor();
        }

        try
        {
            return JsonConvert.DeserializeObject<HistoryCursor>(_cursorProtector.Unprotect(cursor)) ?? new HistoryCursor();
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            throw new ArgumentException("The cursor is not one the history returned.", nameof(cursor), ex);
        }
    }

    /// <summary>Writes where each file's history continues; <c>null</c> when every one is read to the end.</summary>
    protected virtual string WriteCursor(string head, IDictionary<string, HistoryPosition> positions, IList<HistorySource> sources)
    {
        foreach (var source in sources)
        {
            var read = source.Read == source.Nodes.Count;
            positions[GetPositionKey(source.Path)] = new HistoryPosition
            {
                After = read ? source.EndCursor : source.Position.After,
                Skip = read ? 0 : source.Position.Skip + source.Read,
                LiveFound = source.Position.LiveFound || source.LiveFound,
                Done = read && !source.HasMore,
            };
        }

        return positions.Values.All(x => x.Done)
            ? null
            : _cursorProtector.Protect(JsonConvert.SerializeObject(new HistoryCursor { Head = head, Positions = new Dictionary<string, HistoryPosition>(positions, StringComparer.Ordinal) }));
    }

    /// <summary>
    /// Names a file's position in the cursor by a short hash of its path, so the cursor's length does not grow with the paths:
    /// the request validator caps it.
    /// </summary>
    protected virtual string GetPositionKey(string path) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(path)).AsSpan(0, PositionKeyBytes));

    private static string GetOid(JToken node) => node["oid"]?.Value<string>();

    // only the newest change with the live content is live
    private static void AddItem(ContentVersion version, HistorySource source)
    {
        var isLive = !source.LiveFound && source.LiveOid != null &&
                     string.Equals(source.Head.SelectToken("file.oid")?.Value<string>(), source.LiveOid, StringComparison.Ordinal);
        source.LiveFound |= isLive;
        version.Published &= isLive;
        version.Items.Add(source.Item.ToString());
        source.Read++;
    }

    /// <summary>Where the listing continues: the commit its first page read, and each file's position in that commit's history.</summary>
    protected class HistoryCursor
    {
        [JsonProperty("h")]
        public string Head { get; set; }

        [JsonProperty("p")]
        public Dictionary<string, HistoryPosition> Positions { get; set; } = new(StringComparer.Ordinal);
    }

    /// <summary>Where a file's history continues.</summary>
    protected class HistoryPosition
    {
        [JsonProperty("a")]
        public string After { get; set; }

        [JsonProperty("s")]
        public int Skip { get; set; }

        [JsonProperty("l")]
        public bool LiveFound { get; set; }

        [JsonProperty("d")]
        public bool Done { get; set; }
    }

    /// <summary>A file's history as far as it is read.</summary>
    protected class HistorySource(HistoryItem item, string path, HistoryPosition position)
    {
        public HistoryItem Item { get; } = item;

        public string Path { get; } = path;

        public HistoryPosition Position { get; } = position ?? new HistoryPosition();

        public IList<JToken> Nodes { get; set; } = [];

        public int Read { get; set; }

        public bool HasMore { get; set; }

        public string EndCursor { get; set; }

        public string LiveOid { get; set; }

        public bool LiveFound { get; set; }

        public JToken Head => Read < Nodes.Count ? Nodes[Read] : null;
    }
}
