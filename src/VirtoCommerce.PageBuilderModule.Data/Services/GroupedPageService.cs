using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Pages.Core.Events;
using VirtoCommerce.Pages.Core.Models;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.Platform.Data.Infrastructure;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.Services
{
    public class GroupedPageService
        : CrudService<GroupedPageBuilderPage, GroupedPageBuilderPageEntity, GroupedPageBuilderPageChangingEvent,
                GroupedPageBuilderPageChangedEvent>,
          IGroupedPageService
    {
        private const int ExistingGroupsQueryBatchSize = 500;

        private readonly IPageBuilderAssetReferenceIndexService _assetReferenceIndexService;
        private readonly Func<IContentStreamRepository> _contentStreamRepositoryFactory;
        private readonly IEventPublisher _eventPublisher;
        private readonly IPageBuilderSharedComponentReferenceIndexService _sharedComponentReferenceIndexService;
        private readonly ILogger<GroupedPageService> _logger;
        private readonly Func<IPageBuilderModuleRepository> _repositoryFactory;

        public GroupedPageService(
            Func<IPageBuilderModuleRepository> repositoryFactory,
            Func<IContentStreamRepository> contentStreamRepositoryFactory,
            IPlatformMemoryCache platformMemoryCache,
            IEventPublisher eventPublisher,
            ILogger<GroupedPageService> logger,
            IPageBuilderAssetReferenceIndexService assetReferenceIndexService,
            IPageBuilderSharedComponentReferenceIndexService sharedComponentReferenceIndexService)
            : base(repositoryFactory, platformMemoryCache, eventPublisher)
        {
            _repositoryFactory = repositoryFactory;
            _contentStreamRepositoryFactory = contentStreamRepositoryFactory;
            _eventPublisher = eventPublisher;
            _logger = logger;
            _assetReferenceIndexService = assetReferenceIndexService;
            _sharedComponentReferenceIndexService = sharedComponentReferenceIndexService;
        }

        public GroupedPageService(
            Func<IPageBuilderModuleRepository> repositoryFactory,
            Func<IContentStreamRepository> contentStreamRepositoryFactory,
            IPlatformMemoryCache platformMemoryCache,
            IEventPublisher eventPublisher,
            ILogger<GroupedPageService> logger,
            IPageBuilderAssetReferenceIndexService assetReferenceIndexService)
            : this(
                repositoryFactory,
                contentStreamRepositoryFactory,
                platformMemoryCache,
                eventPublisher,
                logger,
                assetReferenceIndexService,
                new LegacySharedComponentReferenceIndexService())
        {
        }

        // The generic CRUD hook runs before its repository transaction, which leaves a gap where a page can
        // acquire a Shared Component reference after store validation. Repositories that support Shared
        // Components keep the validation and commit under the same write locks as raw content writers.
        public override async Task SaveChangesAsync(IList<GroupedPageBuilderPage> models)
        {
            var primaryKeyMap = new PrimaryKeyResolvingMap();
            var changedEntries = new List<GenericChangedEntry<GroupedPageBuilderPage>>();
            var changedEntities = new List<GroupedPageBuilderPageEntity>();
            var originalModels = new List<GroupedPageBuilderPage>();

            using (var repository = _repositoryFactory())
            {
                if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                    repository is not IPageBuilderSharedComponentRepository)
                {
                    await base.SaveChangesAsync(models);
                    return;
                }

                var groupIds = models
                    .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                    .Select(x => x.Id)
                    .ToArray();

                async Task SaveInternalAsync(CancellationToken cancellationToken)
                {
                    var existingEntities = await LoadExistingEntities(repository, models);
                    await PrepareModelsForSaveAsync(
                        models,
                        existingEntities,
                        repository,
                        cancellationToken);
                    await BeforeSaveChanges(models);

                    foreach (var model in models)
                    {
                        var originalEntity = FindExistingEntity(existingEntities, model);
                        var modifiedEntity = FromModel(model, primaryKeyMap);

                        if (originalEntity != null)
                        {
                            repository.TrackModifiedAsAddedForNewChildEntities(originalEntity);

                            var originalModel = ToModel(originalEntity, model: null);
                            originalModels.Add(originalModel);
                            changedEntries.Add(new GenericChangedEntry<GroupedPageBuilderPage>(
                                model,
                                originalModel,
                                EntryState.Modified));
                            modifiedEntity.Patch(originalEntity);
                            originalEntity.ModifiedDate = DateTime.UtcNow;

                            changedEntities.Add(originalEntity);
                        }
                        else
                        {
                            repository.Add(modifiedEntity);
                            changedEntries.Add(new GenericChangedEntry<GroupedPageBuilderPage>(
                                model,
                                EntryState.Added));
                            changedEntities.Add(modifiedEntity);
                        }
                    }

                    await _eventPublisher.Publish(
                        EventFactory<GroupedPageBuilderPageChangingEvent>(changedEntries),
                        cancellationToken);
                    await CommitAsync(repository);
                }

                await writeLockRepository.ExecuteUnderGroupedPageWriteLocksAsync(
                    groupIds,
                    (_, cancellationToken) => SaveInternalAsync(cancellationToken));
            }

            primaryKeyMap.ResolvePrimaryKeys();

            ClearCache(originalModels);
            ClearCache(models);

            foreach (var (changedEntry, index) in changedEntries.Select((x, index) => (x, index)))
            {
                changedEntry.NewEntry = ToModel(changedEntities[index], changedEntry.NewEntry);
            }

            await AfterSaveChangesAsync(models, changedEntries);
            await _eventPublisher.Publish(EventFactory<GroupedPageBuilderPageChangedEvent>(changedEntries));
        }

        protected override async Task<IList<GroupedPageBuilderPageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
        {
            var result = await ((IPageBuilderModuleRepository)repository).GetGroupedPageBuilderPagesByIdsAsync(ids, responseGroup);
            return result;
        }

        private async Task PrepareModelsForSaveAsync(
            IList<GroupedPageBuilderPage> models,
            IList<GroupedPageBuilderPageEntity> existingEntities,
            IPageBuilderModuleRepository repository,
            CancellationToken cancellationToken)
        {
            var existingStores = existingEntities
                .ToDictionary(x => x.Id, x => x.StoreId, StringComparer.OrdinalIgnoreCase);
            var groupsWithSharedComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = existingStores.Keys.ToArray();

            if (ids.Length > 0 && repository is IPageBuilderSharedComponentRepository sharedComponentRepository)
            {
                foreach (var batch in ids.Chunk(ExistingGroupsQueryBatchSize))
                {
                    var referencedGroupIds = await repository.PageBuilderPages
                        .Where(x => batch.Contains(x.GroupId))
                        .Join(
                            sharedComponentRepository.PageBuilderSharedComponentReferences,
                            page => page.Id,
                            reference => reference.PageId,
                            (page, reference) => page.GroupId)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                    groupsWithSharedComponents.UnionWith(referencedGroupIds);
                }
            }

            ValidateStoreImmutability(models, existingStores, groupsWithSharedComponents);
            SynchronizeMovedPageStores(models, existingStores);

            foreach (var group in models)
            {
                var existingPages = existingEntities
                    .FirstOrDefault(x => string.Equals(x.Id, group.Id, StringComparison.OrdinalIgnoreCase))
                    ?.Pages;
                NormalizePublishedPages(group, existingPages);
            }
        }

        internal static void ValidateStoreImmutability(
            IEnumerable<GroupedPageBuilderPage> groups,
            IReadOnlyDictionary<string, string> existingStores,
            ISet<string> groupsWithSharedComponents)
        {
            foreach (var group in groups.Where(x => !string.IsNullOrWhiteSpace(x.Id)))
            {
                if (existingStores.TryGetValue(group.Id, out var existingStoreId) &&
                    groupsWithSharedComponents.Contains(group.Id) &&
                    !string.Equals(existingStoreId, group.StoreId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Page group '{group.Id}' cannot be moved from store '{existingStoreId}' to '{group.StoreId}'.");
                }
            }
        }

        internal static void SynchronizeMovedPageStores(
            IEnumerable<GroupedPageBuilderPage> groups,
            IReadOnlyDictionary<string, string> existingStores)
        {
            foreach (var group in groups.Where(x => !string.IsNullOrWhiteSpace(x.Id)))
            {
                if (!existingStores.TryGetValue(group.Id, out var existingStoreId) ||
                    string.Equals(existingStoreId, group.StoreId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var page in group.Pages)
                {
                    page.StoreId = group.StoreId;
                }
            }
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
        private void NormalizePublishedPages(
            GroupedPageBuilderPage group,
            IEnumerable<PageBuilderPageEntity> existingPages)
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

            var keep = FindNewlyPromotedPage(group.Id, publishedPages, existingPages);

            if (keep == null)
            {
                keep = SelectFallbackPublishedPage(group.Id, publishedPages);
            }

            ArchiveOtherPublishedPages(publishedPages, keep.Id);
        }

        private static PageBuilderPage FindNewlyPromotedPage(
            string groupId,
            IEnumerable<PageBuilderPage> publishedPages,
            IEnumerable<PageBuilderPageEntity> existingPages)
        {
            if (string.IsNullOrEmpty(groupId) || existingPages == null)
            {
                return null;
            }

            var existingStatusById = existingPages
                .Where(page => !string.IsNullOrEmpty(page.Id))
                .ToDictionary(page => page.Id, page => page.Status);

            var newlyPromoted = publishedPages
                .Where(page => !string.IsNullOrEmpty(page.Id)
                    && existingStatusById.TryGetValue(page.Id, out var oldStatus)
                    && oldStatus != Published)
                .Take(2)
                .ToList();

            return newlyPromoted.Count == 1 ? newlyPromoted[0] : null;
        }

        private PageBuilderPage SelectFallbackPublishedPage(
            string groupId,
            IEnumerable<PageBuilderPage> publishedPages)
        {
            var keep = publishedPages.OrderByDescending(page => page.CreatedDate).First();
            _logger.LogWarning(
                "Group '{GroupId}' has multiple Published pages without a clear status transition. " +
                "Keeping page '{KeepId}' (newest CreatedDate) and demoting the rest to Archived.",
                groupId,
                keep.Id);
            return keep;
        }

        private static void ArchiveOtherPublishedPages(
            IEnumerable<PageBuilderPage> publishedPages,
            string keepId)
        {
            foreach (var page in publishedPages.Where(page => page.Id != keepId))
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

        public async Task<bool> LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
        {
            await using var repository = _contentStreamRepositoryFactory();

            // Deliberately not disposed: disposing flushes, and flushing an HTTP response body commits the
            // status line, which would make the caller's fall-through to the next candidate — or to 404 —
            // impossible. It would also emit the UTF8 preamble for a page that turned out to have no content.
            // The writer holds no unmanaged resources and the stream is left open either way.
            var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 8192, leaveOpen: true);
            var found = await repository.TryLoadBinaryAsync(pageId, writer, cancellationToken);
            if (found)
            {
                await writer.FlushAsync(cancellationToken);
            }

            return found;
        }

        public async Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var content = await reader.ReadToEndAsync(cancellationToken);
            await using var repository = _contentStreamRepositoryFactory();
            using var contentReader = new StringReader(content);

            if (repository is ITransactionalContentStreamRepository transactionalRepository)
            {
                var pageStoreId = await GetPageStoreIdForContentWriteAsync(pageId, cancellationToken);
                await transactionalRepository.SaveBinaryAsync(
                    pageId,
                    contentReader,
                    async (dbContext, transactionCancellationToken) =>
                    {
                        await PageBuilderPageIndexing.RebuildAfterRawContentWriteAsync(
                            dbContext,
                            pageId,
                            content,
                            pageStoreId,
                            transactionCancellationToken);
                    },
                    cancellationToken);
                return;
            }

            // Preserve the original extension contract for ordinary pages, but never expose Shared Components
            // to a split raw-content/index write. External providers opt in by implementing the transactional
            // contract; otherwise a component delete can race this write and leave irreconcilable references.
            var previousContent = await LoadRepositoryContentAsync(repository, pageId, cancellationToken);
            EnsureNonTransactionalContentSupported(previousContent, content);
            await repository.SaveBinaryAsync(pageId, contentReader, cancellationToken);
            await _sharedComponentReferenceIndexService.RebuildPageIndexAsync(pageId, content, cancellationToken);
            await _assetReferenceIndexService.RebuildPageIndexAsync(pageId, content, cancellationToken);
        }

        public async Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default)
        {
            await using var repository = _contentStreamRepositoryFactory();
            if (repository is ITransactionalContentStreamRepository transactionalRepository)
            {
                var targetStoreId = await GetPageStoreIdForContentWriteAsync(targetPageId, cancellationToken);
                await transactionalRepository.CopyContentAsync(
                    sourcePageId,
                    targetPageId,
                    async (dbContext, transactionCancellationToken) =>
                    {
                        var copiedContent = await dbContext.Set<PageBuilderContentEntity>()
                            .AsNoTracking()
                            .Where(x => x.Id == targetPageId)
                            .Select(x => x.PageContent)
                            .FirstOrDefaultAsync(transactionCancellationToken);
                        await PageBuilderPageIndexing.RebuildAfterRawContentWriteAsync(
                            dbContext,
                            targetPageId,
                            copiedContent,
                            targetStoreId,
                            transactionCancellationToken);
                    },
                    cancellationToken);
                return;
            }

            // The legacy copy contract remains available for ordinary content only. Shared Component copies
            // require raw content and both indexes to commit together.
            var sourceContent = await LoadRepositoryContentAsync(repository, sourcePageId, cancellationToken);
            var targetContent = await LoadRepositoryContentAsync(repository, targetPageId, cancellationToken);
            EnsureNonTransactionalContentSupported(sourceContent, targetContent);
            await repository.CopyContentAsync(sourcePageId, targetPageId, cancellationToken);

            var copiedContent = await LoadRepositoryContentAsync(repository, targetPageId, cancellationToken);
            await _sharedComponentReferenceIndexService.RebuildPageIndexAsync(targetPageId, copiedContent, cancellationToken);
            await _assetReferenceIndexService.RebuildPageIndexAsync(targetPageId, copiedContent, cancellationToken);
        }

        private async Task<string> GetPageStoreIdForContentWriteAsync(
            string pageId,
            CancellationToken cancellationToken)
        {
            using var repository = _repositoryFactory();
            var pageStoreId = await repository.PageBuilderPages
                .Where(x => x.Id == pageId)
                .Join(
                    repository.GroupedPageBuilderPages,
                    page => page.GroupId,
                    group => group.Id,
                    (page, group) => page.StoreId ?? group.StoreId)
                .FirstOrDefaultAsync(cancellationToken);

            return pageStoreId ?? throw new KeyNotFoundException($"Page '{pageId}' was not found.");
        }

        public async Task<bool> TryDeleteEmptyDraftAsync(
            string pageId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pageId);

            var deleted = false;
            var groupId = (string)null;
            using (var repository = _repositoryFactory())
            {
                if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                    repository is not IPageBuilderSharedComponentRepository sharedComponentRepository)
                {
                    return false;
                }

                await writeLockRepository.ExecuteUnderPageWriteLocksAsync(
                    [pageId],
                    async (dbContext, transactionCancellationToken) =>
                    {
                        // A successful concurrent content writer holds this same row lock. Rechecking after the
                        // lock prevents failed-request cleanup from deleting a draft another request filled.
                        var page = await repository.PageBuilderPages
                            .FirstOrDefaultAsync(x => x.Id == pageId, transactionCancellationToken);
                        if (page == null || page.Status != Draft)
                        {
                            return;
                        }

                        var hasContent = await dbContext.Set<PageBuilderContentEntity>()
                            .AnyAsync(
                                x => x.Id == pageId && x.PageContent != null,
                                transactionCancellationToken);
                        var hasSharedComponentReferences = await sharedComponentRepository.PageBuilderSharedComponentReferences
                            .AnyAsync(x => x.PageId == pageId, transactionCancellationToken);
                        if (hasContent || hasSharedComponentReferences)
                        {
                            return;
                        }

                        groupId = page.GroupId;
                        repository.Remove(page);
                        await repository.UnitOfWork.CommitAsync();
                        deleted = true;
                    },
                    cancellationToken);
            }

            if (deleted)
            {
                GenericCachingRegion<PageBuilderPage>.ExpireTokenForKey(pageId);
                GenericSearchCachingRegion<PageBuilderPage>.ExpireRegion();
                GenericSearchCachingRegion<GroupedPageBuilderPage>.ExpireRegion();
                if (!string.IsNullOrWhiteSpace(groupId))
                {
                    GenericCachingRegion<GroupedPageBuilderPage>.ExpireTokenForKey(groupId);
                }

                // This rollback path removes a draft that was already announced to Pages by the grouped save.
                // A normal PageBuilder delete maps to Archive, so emit the explicit hard-delete operation here.
                var pageDocument = AbstractTypeFactory<PageDocument>.TryCreateInstance();
                pageDocument.Id = pageId;

                var pagesEvent = AbstractTypeFactory<PagesDomainEvent>.TryCreateInstance();
                pagesEvent.Id = pageId;
                pagesEvent.Page = pageDocument;
                pagesEvent.Operation = PageOperation.Delete;

                // The row deletion has already committed, so request cancellation must not suppress the
                // corresponding Pages delete notification and leave the downstream index stale.
                await _eventPublisher.Publish(pagesEvent, CancellationToken.None);
            }

            return deleted;
        }

        internal static void EnsureNonTransactionalContentSupported(params string[] contents)
        {
            if (contents.Any(PageBuilderSharedComponentReferenceMatcher.HasReferences))
            {
                throw new NotSupportedException(
                    "Shared Component page content requires an ITransactionalContentStreamRepository implementation.");
            }
        }

        private static async Task<string> LoadRepositoryContentAsync(
            IContentStreamRepository repository,
            string pageId,
            CancellationToken cancellationToken)
        {
            using var writer = new StringWriter();
            await repository.TryLoadBinaryAsync(pageId, writer, cancellationToken);
            return writer.ToString();
        }

        private sealed class LegacySharedComponentReferenceIndexService
            : IPageBuilderSharedComponentReferenceIndexService
        {
            public Task ValidateReferencesForStoreAsync(
                string storeId,
                IEnumerable<string> contents,
                CancellationToken cancellationToken = default)
            {
                EnsureNonTransactionalContentSupported(contents?.ToArray() ?? []);
                return Task.CompletedTask;
            }

            public Task RebuildPageIndexAsync(
                string pageId,
                string content,
                CancellationToken cancellationToken = default)
            {
                EnsureNonTransactionalContentSupported(content);
                return Task.CompletedTask;
            }

            public Task<IList<string>> GetPageIdsAsync(
                IEnumerable<string> sharedComponentIds,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IList<string>>([]);
            }
        }
    }
}
