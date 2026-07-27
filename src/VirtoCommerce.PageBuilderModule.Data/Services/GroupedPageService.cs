using System.Text;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.Services
{
    public class GroupedPageService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        Func<IContentStreamRepository> contentStreamRepositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher,
        ILogger<GroupedPageService> logger,
        IPageBuilderAssetReferenceIndexService assetReferenceIndexService)
        : CrudService<GroupedPageBuilderPage, GroupedPageBuilderPageEntity, GroupedPageBuilderPageChangingEvent,
                GroupedPageBuilderPageChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
            IGroupedPageService
    {
        protected override async Task<IList<GroupedPageBuilderPageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
        {
            var result = await ((IPageBuilderModuleRepository)repository).GetGroupedPageBuilderPagesByIdsAsync(ids, responseGroup);
            return result;
        }

        protected override async Task BeforeSaveChanges(IList<GroupedPageBuilderPage> models)
        {
            foreach (var group in models)
            {
                await NormalizePublishedPages(group);
            }

            await base.BeforeSaveChanges(models);
        }

        // A grouped save changes the status of child pages (publish/unpublish/archive), but the base
        // ClearCache only expires GroupedPageBuilderPage regions. Read paths that hydrate individual
        // pages — notably PageBuilderPageSearchService used by the search-index reindex provider —
        // go through PageBuilderPage caches, which would otherwise keep serving the pre-change status
        // until they expire. That makes a reindex write stale statuses to the index (the event-driven
        // path is unaffected because it reads the freshly saved entry, not the cache). Invalidate the
        // affected pages' caches here so a reindex immediately reflects the new status.
        protected override void ClearCache(IList<GroupedPageBuilderPage> models)
        {
            base.ClearCache(models);

            var pageIds = models
                .Where(x => x?.Pages != null)
                .SelectMany(x => x.Pages)
                .Select(x => x.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            if (pageIds.Count == 0)
            {
                return;
            }

            foreach (var id in pageIds)
            {
                GenericCachingRegion<PageBuilderPage>.ExpireTokenForKey(id);
            }

            GenericSearchCachingRegion<PageBuilderPage>.ExpireRegion();
        }

        // Enforces invariant: at most one Published page per group.
        // If multiple Published exist, picks the page that is transitioning to Published in this save
        // (compared against DB state) and demotes the rest to Archived. This handles the legitimate
        // PublishGroup flow silently. If no clear transition exists (data anomaly from import/migration),
        // falls back to "newest by CreatedDate" and logs a warning.
        // Reference queries read page status from PageBuilderPage, so demoted pages do not need reference metadata refresh.
        private async Task NormalizePublishedPages(GroupedPageBuilderPage group)
        {
            if (group?.Pages == null)
            {
                return;
            }

            var publishedPages = group.Pages.Where(x => x.Status == Published).ToList();
            if (publishedPages.Count <= 1)
            {
                return;
            }

            PageBuilderPage keep = null;

            if (!string.IsNullOrEmpty(group.Id))
            {
                using var repository = repositoryFactory();
                var existingEntities = await repository.GetGroupedPageBuilderPagesByIdsAsync([group.Id], null);
                var existingPages = existingEntities.FirstOrDefault()?.Pages;
                if (existingPages != null)
                {
                    var existingStatusById = existingPages
                        .Where(p => !string.IsNullOrEmpty(p.Id))
                        .ToDictionary(p => p.Id, p => p.Status);

                    var newlyPromoted = publishedPages
                        .Where(p => !string.IsNullOrEmpty(p.Id)
                            && existingStatusById.TryGetValue(p.Id, out var oldStatus)
                            && oldStatus != Published)
                        .ToList();

                    if (newlyPromoted.Count == 1)
                    {
                        keep = newlyPromoted[0];
                    }
                }
            }

            if (keep == null)
            {
                keep = publishedPages.OrderByDescending(x => x.CreatedDate).First();
                logger.LogWarning("Group '{GroupId}' has multiple Published pages without a clear status transition. " +
                    "Keeping page '{KeepId}' (newest CreatedDate) and demoting the rest to Archived.",
                    group.Id, keep.Id);
            }

            foreach (var page in publishedPages.Where(p => p.Id != keep.Id))
            {
                page.Status = Archived;
            }
        }

        public async Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default)
        {
            await using var memoryStream = new MemoryStream();
            await LoadContentToStreamAsync(pageId, memoryStream, cancellationToken);
            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        public async Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default)
        {
            await using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await SaveStreamAsContentAsync(pageId, memoryStream, cancellationToken);
        }

        public async Task LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
        {
            var repository = contentStreamRepositoryFactory();
            await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 8192, leaveOpen: true);
            await repository.LoadBinaryAsync(pageId, writer, cancellationToken);
        }

        public async Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var content = await reader.ReadToEndAsync(cancellationToken);
            var repository = contentStreamRepositoryFactory();
            using var contentReader = new StringReader(content);
            await repository.SaveBinaryAsync(pageId, contentReader, cancellationToken);

            await assetReferenceIndexService.RebuildPageIndexAsync(pageId, content, cancellationToken);
        }

        public async Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default)
        {
            await using var memoryStream = new MemoryStream();
            await LoadContentToStreamAsync(sourcePageId, memoryStream, cancellationToken);
            memoryStream.Position = 0;
            await SaveStreamAsContentAsync(targetPageId, memoryStream, cancellationToken);
        }
    }
}
