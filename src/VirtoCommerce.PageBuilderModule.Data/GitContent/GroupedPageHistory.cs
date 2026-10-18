using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// The version history of a database page (a site's page list): every save, publish, restore and discard
    /// is also committed to the site's repository, on its base branch, as "database-pages/{groupId}.json".
    /// The database stays the page's source of truth — the storefront is served from it, through the search
    /// index — and the repository is its history (see <see cref="SiteHistory"/>).
    /// </summary>
    public class GroupedPageHistory(
        IGitContentPolicy policy,
        IGitContentRepository repository,
        IHttpClientFactory httpClientFactory,
        IGroupedPageService groupedPageService,
        IOptions<GitContentOptions> options,
        ILogger<GroupedPageHistory> logger)
        : SiteHistory(policy, repository, httpClientFactory, options, logger)
    {
        public const string Folder = "database-pages";

        public static string PathOf(string groupId) => $"{Folder}/{groupId}.json";

        /// <summary>
        /// Commits the page's content as it now stands, unless the repository already has exactly that.
        /// Does nothing for a store without version history.
        /// </summary>
        public virtual Task RecordAsync(GroupedPageBuilderPage group, string content, string summary, ClaimsPrincipal user, string save, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(group);
            return RecordFileAsync(group.StoreId, PathOf(group.Id), About(group), content, summary, user, save, cancellationToken);
        }

        /// <summary>
        /// The page's changes, newest first, a page of <paramref name="take"/> at a time (<paramref name="after"/>
        /// continues); <c>null</c> for a store without version history.
        /// </summary>
        public virtual async Task<GroupedPageHistoryResult> GetAsync(GroupedPageBuilderPage group, string after = null, int take = 20, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(group);
            return await GetFileHistoryAsync(group.StoreId, PathOf(group.Id), About(group), await LoadPublishedContentAsync(group, cancellationToken), after, take, cancellationToken);
        }

        // which page a change is about: needed on GitHub, where the repository holds every page of the site
        private static string About(GroupedPageBuilderPage group) => $": \"{group.Name}\" ({group.Permalink})";

        /// <summary>The page's content at a change; <c>null</c> when there is no such change or no history.</summary>
        public virtual Task<string> ReadAsync(GroupedPageBuilderPage group, string sha, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(group);
            return ReadFileAsync(group.StoreId, PathOf(group.Id), sha, cancellationToken);
        }

        /// <summary>What the storefront shows of the page; <c>null</c> while it is not published.</summary>
        public virtual async Task<string> LoadPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default)
        {
            var page = group.Pages?.FirstOrDefault(x => x.Status == Published);
            return page == null ? null : await groupedPageService.LoadContent(page.Id, cancellationToken);
        }
    }
}
