using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// A page's version list, assembled with a single GitHub GraphQL query: the production branch's
    /// history of the file, plus every branch's history of the same file.
    /// <para>
    /// GraphQL rather than REST because REST has no way to ask "commits touching this file across all
    /// refs" — it would take one call per branch. Measured against the content repository, one query
    /// covering all 47 branches costs 1 point of the 5000 per hour, which is what makes a version list
    /// affordable at all.
    /// </para>
    /// <para>
    /// Branches are enumerated whole, without a name filter: filtering by prefix costs the same and would
    /// hide a draft made on a branch named anything else, which is the divergence this list exists to
    /// expose.
    /// </para>
    /// </summary>
    public class GitHubContentHistory : IGitContentHistory
    {
        // The published history and each branch's history of the same path, in one round trip. Branch
        // names come back without the "refs/heads/" prefix, so "designer/john/about-us" stays readable.
        private const string HistoryQuery = """
            query($owner:String!,$name:String!,$path:String!,$base:String!,$take:Int!,$refs:Int!,$perBranch:Int!,$after:String){
              repository(owner:$owner,name:$name){
                published: ref(qualifiedName:$base){
                  target{ ... on Commit {
                    history(path:$path, first:$take){
                      nodes{ oid committedDate messageHeadline messageBody changedFilesIfAvailable
                             author{ name email user{ login } } } } } } }
                branches: refs(refPrefix:"refs/heads/", first:$refs, after:$after){
                  pageInfo{ hasNextPage endCursor }
                  nodes{ name target{ ... on Commit {
                    history(path:$path, first:$perBranch){
                      nodes{ oid committedDate messageHeadline messageBody changedFilesIfAvailable
                             author{ name email user{ login } } } } } } } }
              }
            }
            """;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly GitContentOptions _options;

        public GitHubContentHistory(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IOptions<GitContentOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _options = options.Value;
        }

        // The arguments are checked here rather than in the async part below, so a call that cannot work
        // throws where it was made instead of when its task is finally awaited.
        public Task<GitPageHistory> GetHistoryAsync(string repoPath, GitHistoryQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repoPath);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentException.ThrowIfNullOrWhiteSpace(query.BaseBranch);

            return GetHistoryCoreAsync(repoPath, query, cancellationToken);
        }

        private async Task<GitPageHistory> GetHistoryCoreAsync(string repoPath, GitHistoryQuery query, CancellationToken cancellationToken)
        {
            var (owner, name) = SplitRepository();
            var depth = query.PublishedDepth ?? _options.PublishedHistoryDepth;

            var cacheKey = CacheKey(repoPath, query, depth);
            if (_memoryCache.TryGetValue<GitPageHistory>(cacheKey, out var cached))
            {
                return cached;
            }

            var body = await PostQueryAsync(owner, name, repoPath, query, depth, cancellationToken);
            var history = Assemble(body, query.BaseBranch);

            _memoryCache.Set(cacheKey, history, _options.HistoryCacheExpiration);

            return history;
        }

        public void Invalidate(string repoPath)
        {
            if (string.IsNullOrWhiteSpace(repoPath))
            {
                return;
            }

            // The list is cached per base branch, depth and cursor, and IMemoryCache cannot drop a family
            // of keys — so the keys carry a generation number for the page and this moves it on. Bumping
            // a counter leaves the stale entries to expire on their own instead of being read again.
            _memoryCache.Set(GenerationKey(repoPath), Generation(repoPath) + 1, NeverEvict);
        }

        private async Task<JObject> PostQueryAsync(string owner, string name, string repoPath, GitHistoryQuery query, int depth, CancellationToken cancellationToken)
        {
            var request = new JObject
            {
                ["query"] = HistoryQuery,
                ["variables"] = new JObject
                {
                    ["owner"] = owner,
                    ["name"] = name,
                    ["path"] = repoPath,
                    ["base"] = query.BaseBranch,
                    ["take"] = depth,
                    ["refs"] = _options.MaxBranches,
                    ["perBranch"] = _options.CommitsPerBranch,
                    ["after"] = query.After,
                },
            };

            var client = _httpClientFactory.CreateClient(GitHubContentRepository.HttpClientName);
            using var response = await client.PostAsync(_options.GraphQlUrl,
                new StringContent(request.ToString(), Encoding.UTF8, "application/json"), cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw Failed($"{(int)response.StatusCode} {response.ReasonPhrase}. {Trim(content)}", repoPath);
            }

            var body = JObject.Parse(content);

            // GraphQL reports failures inside a 200 response. An empty version list and a failed query
            // look the same to a caller, and the difference matters: one means "no other versions", the
            // other means "we do not know".
            var errors = body["errors"] as JArray;
            if (errors?.Count > 0)
            {
                var messages = string.Join("; ", errors.Select(error => error["message"]?.Value<string>()).Where(message => message != null));
                throw Failed(Trim(messages), repoPath);
            }

            return body;
        }

        private GitPageHistory Assemble(JObject body, string baseBranch)
        {
            var repository = Field(Field(body, "data"), "repository");
            var branches = Field(repository, "branches");

            var accumulated = new Dictionary<string, Accumulated>(StringComparer.OrdinalIgnoreCase);

            // The production branch first, so the shas it contains are known to be published before any
            // branch is looked at.
            foreach (var commit in Commits(Field(Field(repository, "published"), "target")))
            {
                Add(accumulated, commit, branch: null, published: true);
            }

            foreach (var branch in Field(branches, "nodes") as JArray ?? [])
            {
                var branchName = Field(branch, "name")?.Value<string>();
                if (string.IsNullOrEmpty(branchName) || string.Equals(branchName, baseBranch, StringComparison.Ordinal))
                {
                    // the production branch is already accounted for, and reading it twice would list it
                    // as a branch of its own commits
                    continue;
                }

                foreach (var commit in Commits(Field(branch, "target")))
                {
                    Add(accumulated, commit, branchName, published: false);
                }
            }

            // Unpublished versions first: they are the reason to open the list at all — someone's work
            // that production does not have yet.
            var versions = accumulated.Values
                .OrderBy(version => version.Published)
                .ThenByDescending(version => version.Date ?? DateTime.MinValue)
                .Select(version => version.ToVersion(baseBranch, _options.BulkCommitFileCount))
                .ToList();

            return new GitPageHistory
            {
                Versions = versions,
                Truncated = Field(Field(branches, "pageInfo"), "hasNextPage")?.Value<bool>() ?? false,
                EndCursor = Field(Field(branches, "pageInfo"), "endCursor")?.Value<string>(),
            };
        }

        private static JArray Commits(JToken target) => Field(Field(target, "history"), "nodes") as JArray ?? [];

        /// <summary>
        /// A field of a json object, or <c>null</c> when there is no object there at all. GraphQL answers
        /// an absent thing with json <c>null</c> rather than by leaving the field out — a page the
        /// production branch does not have comes back as <c>"published": null</c> — and indexing into that
        /// null throws instead of yielding nothing.
        /// </summary>
        private static JToken Field(JToken token, string name) => token is JObject json ? json[name] : null;

        /// <summary>
        /// Folds one commit into the version list. The same sha arrives many times — a commit is
        /// reachable from every branch cut after it — so each occurrence only adds the branch it was
        /// found on.
        /// </summary>
        private static void Add(Dictionary<string, Accumulated> accumulated, JToken commit, string branch, bool published)
        {
            var sha = Field(commit, "oid")?.Value<string>();
            if (string.IsNullOrEmpty(sha))
            {
                return;
            }

            if (!accumulated.TryGetValue(sha, out var version))
            {
                var author = Field(commit, "author");
                version = new Accumulated
                {
                    Sha = sha,
                    Date = ReadDate(Field(commit, "committedDate")),
                    Message = Field(commit, "messageHeadline")?.Value<string>(),
                    AuthorName = Field(author, "name")?.Value<string>(),
                    AuthorEmail = Field(author, "email")?.Value<string>(),
                    AuthorLogin = Field(Field(author, "user"), "login")?.Value<string>(),
                    VcUser = GitCommitMessage.ReadTrailer(Field(commit, "messageBody")?.Value<string>(), GitCommitMessage.VcUserTrailer),
                    ChangedFiles = Field(commit, "changedFilesIfAvailable")?.Value<int?>(),
                };
                accumulated.Add(sha, version);
            }

            version.Published |= published;
            if (branch != null)
            {
                version.Branches.Add(branch);
            }
        }

        /// <summary>
        /// Both shapes the timestamp can arrive in, and never through the host's culture: Newtonsoft turns
        /// an ISO timestamp into a <see cref="DateTime"/> while parsing the response, and formatting that
        /// back into a string to re-parse it reordered day and month on a machine that writes dates
        /// day-first — "2026-08-11" came out as 8 November.
        /// </summary>
        private static DateTime? ReadDate(JToken value) => value switch
        {
            JValue { Value: DateTime dateTime } => dateTime.ToUniversalTime(),
            JValue { Value: DateTimeOffset offset } => offset.UtcDateTime,
            JValue { Value: string text } when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed.UtcDateTime,
            _ => null,
        };

        private (string Owner, string Name) SplitRepository()
        {
            var repository = _options.Repository;
            var separator = repository?.IndexOf('/') ?? -1;
            if (separator <= 0 || separator == repository.Length - 1)
            {
                throw new InvalidOperationException(
                    $"Git content is not configured: set {GitContentOptions.SectionName}:Repository to \"owner/name\".");
            }

            return (repository[..separator], repository[(separator + 1)..]);
        }

        private string CacheKey(string repoPath, GitHistoryQuery query, int depth) =>
            $"{nameof(GitHubContentHistory)}:{_options.Repository}:{Generation(repoPath)}:{query.BaseBranch}:{depth}:{query.After}:{repoPath}";

        private string GenerationKey(string repoPath) => $"{nameof(GitHubContentHistory)}:generation:{_options.Repository}:{repoPath}";

        private long Generation(string repoPath) => _memoryCache.GetOrCreate(GenerationKey(repoPath), entry =>
        {
            entry.SetOptions(NeverEvict);
            return 0L;
        });

        // A page's generation counter has to outlive its cached list, or invalidation would be undone by
        // eviction: a dropped counter restarts at zero and points back at the entry it was meant to retire.
        private static readonly MemoryCacheEntryOptions NeverEvict = new() { Priority = CacheItemPriority.NeverRemove };

        // Enough of a GitHub error body to recognise the failure by, rather than a page of json in a log line.
        private const int MaxErrorDetailsLength = 500;

        private static string Trim(string details) =>
            details?.Length > MaxErrorDetailsLength ? details[..MaxErrorDetailsLength] : details;

        private static HttpRequestException Failed(string details, string repoPath) =>
            new($"GitHub GraphQL request for the history of \"{repoPath}\" failed: {details}");

        /// <summary>One sha, and the branches it has been seen on so far.</summary>
        private sealed class Accumulated
        {
            public string Sha { get; init; }
            public DateTime? Date { get; init; }
            public string Message { get; init; }
            public string AuthorName { get; init; }
            public string AuthorEmail { get; init; }
            public string AuthorLogin { get; init; }
            public string VcUser { get; init; }
            public int? ChangedFiles { get; init; }
            public bool Published { get; set; }
            public SortedSet<string> Branches { get; } = new(StringComparer.Ordinal);

            public GitPageVersion ToVersion(string baseBranch, int bulkFileCount) => new()
            {
                Sha = Sha,
                Date = Date,
                Message = Message,
                AuthorName = AuthorName,
                AuthorEmail = AuthorEmail,
                AuthorLogin = AuthorLogin,
                VcUser = VcUser,
                Published = Published,
                ChangedFiles = ChangedFiles,
                Bulk = ChangedFiles > bulkFileCount,
                // the production branch is what "published" means, so it leads the list of refs
                Branches = Published ? [baseBranch, .. Branches] : [.. Branches],
            };
        }
    }
}
