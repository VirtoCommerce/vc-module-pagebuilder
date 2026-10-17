using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// The version history of a database page (a site's page list): every save, publish, restore and discard
    /// is also committed to the site's repository, on its base branch, as "database-pages/{groupId}.json".
    /// <para>
    /// The database stays the page's source of truth — the storefront is served from it, through the search
    /// index — and the repository is its history: one linear list of changes per page, newest first. Which
    /// of them is live is not stored anywhere: it is the newest change whose file is byte for byte the
    /// published content, because a commit holds exactly the bytes the database saved.
    /// </para>
    /// <para>
    /// Recording is best effort. A GitHub outage costs the history an entry, never the editor a save.
    /// </para>
    /// </summary>
    public class GroupedPageHistory(
        IGitContentPolicy policy,
        IGitContentRepository repository,
        IHttpClientFactory httpClientFactory,
        IGroupedPageService groupedPageService,
        IOptions<GitContentOptions> options,
        ILogger<GroupedPageHistory> logger)
    {
        public const string Folder = "database-pages";

        private const string HistoryQuery = """
            query($owner:String!,$name:String!,$branch:String!,$path:String!,$take:Int!,$after:String){
              repository(owner:$owner,name:$name){
                ref(qualifiedName:$branch){
                  target{ ... on Commit{
                    history(path:$path, first:$take, after:$after){
                      pageInfo{ hasNextPage endCursor }
                      nodes{ oid committedDate messageHeadline messageBody author{ name email user{ login } } file(path:$path){ oid } }
                    }
                  } }
                }
              }
            }
            """;

        public static string PathOf(string groupId) => $"{Folder}/{groupId}.json";

        /// <summary>
        /// Commits the page's content as it now stands, unless the repository already has exactly that.
        /// Does nothing for a store without version history.
        /// </summary>
        public virtual async Task RecordAsync(GroupedPageBuilderPage group, string content, string summary, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(group);

            try
            {
                var site = await policy.GetRepositoryAsync(group.StoreId, cancellationToken);
                if (site == null || content == null)
                {
                    return;
                }

                var settings = options.Value;
                var userName = user?.Identity?.Name;
                var author = new GitCommitAuthor
                {
                    Name = string.IsNullOrEmpty(userName) ? settings.FallbackAuthorName : userName,
                    Email = user?.FindFirstValue(ClaimTypes.Email) ?? settings.FallbackAuthorEmail,
                };
                var message = GitCommitMessage.WithVcUser($"{summary}{About(group)}", userName);

                await repository.CommitFileAsync(site, PathOf(group.Id), content, settings.BaseBranch, message, author, cancellationToken);
            }
            catch (Exception failure) when (failure is HttpRequestException or InvalidOperationException)
            {
                logger.LogWarning(failure, "Version history of page {GroupId} (store {StoreId}) missed a change: {Summary}.", group.Id, group.StoreId, summary);
            }
        }

        /// <summary>
        /// The page's changes, newest first, a page of <paramref name="take"/> at a time (<paramref name="after"/>
        /// continues); <c>null</c> for a store without version history.
        /// </summary>
        public virtual async Task<GroupedPageHistoryResult> GetAsync(GroupedPageBuilderPage group, string after = null, int take = 20, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(group);

            var site = await policy.GetRepositoryAsync(group.StoreId, cancellationToken);
            if (site == null)
            {
                return null;
            }

            var settings = options.Value;
            var owner = site.Split('/')[0];
            using var client = httpClientFactory.CreateClient(GitHubContentRepository.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.GraphQlUrl)
            {
                Content = new StringContent(new JObject
                {
                    ["query"] = HistoryQuery,
                    ["variables"] = new JObject
                    {
                        ["owner"] = owner,
                        ["name"] = site[(owner.Length + 1)..],
                        ["branch"] = $"refs/heads/{settings.BaseBranch}",
                        ["path"] = PathOf(group.Id),
                        ["take"] = Math.Clamp(take, 1, 100),
                        ["after"] = after,
                    },
                }.ToString(Formatting.None), Encoding.UTF8, "application/json"),
            };
            // the GraphQL url names no repository: the App's token is the site organization's
            request.Options.Set(GitHubAuthHandler.Owner, owner);

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"GitHub refused the version history of \"{PathOf(group.Id)}\": {(int)response.StatusCode} {response.ReasonPhrase}.", null, response.StatusCode);
            }

            var answer = JObject.Parse(body);
            if (answer["errors"] is JArray { Count: > 0 } errors)
            {
                throw new InvalidOperationException($"GitHub refused the version history of \"{PathOf(group.Id)}\": {errors[0]?["message"]}");
            }

            var history = answer.SelectToken("data.repository.ref.target.history");
            var publishedOid = BlobOid(await LoadPublishedContentAsync(group));
            var published = false;
            var versions = new List<GroupedPageVersion>();
            foreach (var node in history?["nodes"] as JArray ?? [])
            {
                // the newest change holding the live content is the published one; an older change with the
                // same content (published, edited, then discarded back) is history, not live
                var live = !published && publishedOid != null && string.Equals(node.SelectToken("file.oid")?.Value<string>(), publishedOid, StringComparison.Ordinal);
                published |= live;
                versions.Add(new GroupedPageVersion
                {
                    Sha = node["oid"]?.Value<string>(),
                    Date = node["committedDate"]?.Value<DateTime?>(),
                    Message = Summary(node["messageHeadline"]?.Value<string>(), group),
                    VcUser = GitCommitMessage.ReadTrailer(node["messageBody"]?.Value<string>(), GitCommitMessage.VcUserTrailer),
                    AuthorName = node.SelectToken("author.name")?.Value<string>(),
                    AuthorEmail = node.SelectToken("author.email")?.Value<string>(),
                    AuthorLogin = node.SelectToken("author.user.login")?.Value<string>(),
                    Published = live,
                });
            }

            return new GroupedPageHistoryResult
            {
                Versions = versions,
                HasMore = history?.SelectToken("pageInfo.hasNextPage")?.Value<bool>() == true,
                EndCursor = history?.SelectToken("pageInfo.endCursor")?.Value<string>(),
            };
        }

        // which page a change is about: needed on GitHub, where the repository holds every page of the site
        private static string About(GroupedPageBuilderPage group) => $": \"{group.Name}\" ({group.Permalink})";

        // in the page's own history the page goes without saying: "Saved", "Published", "Restored 37edb81"
        private static string Summary(string headline, GroupedPageBuilderPage group) =>
            headline != null && headline.EndsWith(About(group), StringComparison.Ordinal) ? headline[..^About(group).Length] : headline;

        /// <summary>The page's content at a change; <c>null</c> when there is no such change or no history.</summary>
        public virtual async Task<string> ReadAsync(GroupedPageBuilderPage group, string sha, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(group);

            var site = await policy.GetRepositoryAsync(group.StoreId, cancellationToken);
            return site == null ? null : await repository.ReadFileAsync(site, PathOf(group.Id), sha, cancellationToken);
        }

        /// <summary>What the storefront shows of the page; <c>null</c> while it is not published.</summary>
        public virtual async Task<string> LoadPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default)
        {
            var page = group.Pages?.FirstOrDefault(x => x.Status == Published);
            return page == null ? null : await groupedPageService.LoadContent(page.Id, cancellationToken);
        }

        /// <summary>
        /// Git's id of a file holding exactly <paramref name="content"/> — what GitHub reports as the file's oid
        /// at a commit; <c>null</c> for no content.
        /// </summary>
        public static string BlobOid(string content)
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
    }

    /// <summary>One change of a database page: who, when, what, and whether it is the one the storefront shows.</summary>
    public sealed class GroupedPageVersion
    {
        public string Sha { get; init; }

        public string ShortSha => Sha?.Length > 7 ? Sha[..7] : Sha;

        public DateTime? Date { get; init; }

        public string Message { get; init; }

        public string VcUser { get; init; }

        public string AuthorName { get; init; }

        public string AuthorEmail { get; init; }

        public string AuthorLogin { get; init; }

        public bool Published { get; init; }
    }

    public sealed class GroupedPageHistoryResult
    {
        public IList<GroupedPageVersion> Versions { get; init; } = [];

        public bool HasMore { get; init; }

        /// <summary>Pass back as <c>after</c> for the next changes.</summary>
        public string EndCursor { get; init; }
    }
}
