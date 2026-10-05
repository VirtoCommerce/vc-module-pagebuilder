using System.Text;
using System.Text.Json.Nodes;
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
          IGroupedPageService, IGroupedPageContentService
    {
        private const int ExistingGroupsQueryBatchSize = 500;

        private readonly Func<IContentStreamRepository> _contentStreamRepositoryFactory;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<GroupedPageService> _logger;
        private readonly Func<IPageBuilderModuleRepository> _repositoryFactory;

        public GroupedPageService(
            Func<IPageBuilderModuleRepository> repositoryFactory,
            Func<IContentStreamRepository> contentStreamRepositoryFactory,
            IPlatformMemoryCache platformMemoryCache,
            IEventPublisher eventPublisher,
            ILogger<GroupedPageService> logger)
            : base(repositoryFactory, platformMemoryCache, eventPublisher)
        {
            _repositoryFactory = repositoryFactory;
            _contentStreamRepositoryFactory = contentStreamRepositoryFactory;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        // The generic CRUD hook runs before its repository transaction, which leaves a gap where a page can
        // acquire a Shared Component reference after store validation. Repositories that support Shared
        // Components keep the validation and commit under the same write locks as raw content writers.
        public override Task SaveChangesAsync(IList<GroupedPageBuilderPage> models)
        {
            return SaveChangesInternalAsync(models);
        }

        private async Task SaveChangesInternalAsync(
            IList<GroupedPageBuilderPage> models,
            Func<IPageBuilderModuleRepository, IList<GroupedPageBuilderPageEntity>, CancellationToken, Task<PreparedSave>> prepare = null,
            CancellationToken cancellationToken = default)
        {
            var groupedEventEntries = new List<GenericChangedEntry<GroupedPageBuilderPage>>();
            var primaryKeyMap = new PrimaryKeyResolvingMap();
            var changedEntries = new GenericChangedEntry<GroupedPageBuilderPage>[models.Count];
            var changedEntities = new GroupedPageBuilderPageEntity[models.Count];
            var originalModels = new List<GroupedPageBuilderPage>();

            using (var repository = _repositoryFactory())
            {
                var groupIds = models
                    .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                    .Select(x => x.Id)
                    .ToArray();

                async Task SaveInternalAsync(CancellationToken ct)
                {
                    var publishGroupedEvents = true;
                    var existingEntities = await LoadExistingEntities(repository, models);
                    if (prepare != null)
                    {
                        var prepared = await prepare(repository, existingEntities, ct);
                        if (prepared == null)
                        {
                            return;
                        }
                        models = [prepared.Model];
                        publishGroupedEvents = prepared.PublishGroupedEvents;
                    }
                    await PrepareModelsForSaveAsync(
                        models,
                        existingEntities,
                        repository,
                        ct);
                    await BeforeSaveChanges(models);

                    // Load only the content explicitly supplied for update, so Patch changes the tracked
                    // table-split dependent rather than inserting a second one for the same page.
                    var contentPageIds = models.SelectMany(x => x.Pages ?? [])
                        .Where(x => x.Content != null && !string.IsNullOrEmpty(x.Id)).Select(x => x.Id).ToArray();
                    if (contentPageIds.Length > 0)
                    {
                        await repository.PageBuilderContents.Where(x => contentPageIds.Contains(x.Id)).LoadAsync(ct);
                    }

                    ApplyEntityChanges(repository, models, existingEntities, primaryKeyMap, changedEntries, changedEntities, originalModels);

                    if (publishGroupedEvents)
                    {
                        groupedEventEntries.AddRange(changedEntries);
                        await _eventPublisher.Publish(EventFactory<GroupedPageBuilderPageChangingEvent>(changedEntries), ct);
                    }
                    await CommitAsync(repository);
                    primaryKeyMap.ResolvePrimaryKeys();
                    await RebuildWrittenContentIndexesAsync(repository, models, ct);
                }

                await repository.ExecuteUnderGroupedPageWriteLocksAsync(
                    groupIds,
                    SaveInternalAsync,
                    cancellationToken);
            }

            if (changedEntries.All(x => x == null))
            {
                return;
            }

            ClearCache(originalModels);
            ClearCache(models);

            for (var index = 0; index < changedEntries.Length; index++)
            {
                var changedEntry = changedEntries[index];
                changedEntry.NewEntry = ToModel(changedEntities[index], changedEntry.NewEntry);
            }

            await AfterSaveChangesAsync(models, changedEntries);
            if (groupedEventEntries.Count > 0)
            {
                await _eventPublisher.Publish(EventFactory<GroupedPageBuilderPageChangedEvent>(groupedEventEntries), CancellationToken.None);
            }
        }

        private void ApplyEntityChanges(
            IPageBuilderModuleRepository repository, IList<GroupedPageBuilderPage> models,
            IList<GroupedPageBuilderPageEntity> existingEntities, PrimaryKeyResolvingMap primaryKeyMap,
            GenericChangedEntry<GroupedPageBuilderPage>[] changedEntries, GroupedPageBuilderPageEntity[] changedEntities,
            List<GroupedPageBuilderPage> originalModels)
        {
            for (var index = 0; index < models.Count; index++)
            {
                var model = models[index];
                var originalEntity = FindExistingEntity(existingEntities, model);
                var modifiedEntity = FromModel(model, primaryKeyMap);
                if (originalEntity != null)
                {
                    repository.TrackModifiedAsAddedForNewChildEntities(originalEntity);
                    var originalModel = ToModel(originalEntity, model: null);
                    originalModels.Add(originalModel);
                    changedEntries[index] = new GenericChangedEntry<GroupedPageBuilderPage>(model, originalModel, EntryState.Modified);
                    modifiedEntity.Patch(originalEntity);
                    originalEntity.ModifiedDate = DateTime.UtcNow;
                    changedEntities[index] = originalEntity;
                }
                else
                {
                    repository.Add(modifiedEntity);
                    changedEntries[index] = new GenericChangedEntry<GroupedPageBuilderPage>(model, EntryState.Added);
                    changedEntities[index] = modifiedEntity;
                }
            }
        }

        private static async Task RebuildWrittenContentIndexesAsync(
            IPageBuilderModuleRepository repository, IList<GroupedPageBuilderPage> models, CancellationToken cancellationToken)
        {
            foreach (var group in models)
            {
                foreach (var page in (group.Pages ?? []).Where(x => x.Content != null))
                {
                    if (repository is IPageBuilderContentIndexRepository indexes)
                    {
                        await indexes.RebuildPageContentIndexesAsync(page.Id, page.Content, group.StoreId, cancellationToken);
                    }
                    else
                    {
                        await RebuildIndexesWithRepositoryAsync(repository, page, group.StoreId, cancellationToken);
                    }
                }
            }
        }

        // Older repository decorators expose the original query, lock and indexing contracts.
        private static async Task RebuildIndexesWithRepositoryAsync(
            IPageBuilderModuleRepository repository, PageBuilderPage page, string storeId, CancellationToken cancellationToken)
        {
            var componentIds = PageBuilderWriteLock.OrderIds(PageBuilderSharedComponentReferenceMatcher.ExtractReferences(page.Content));
            var rebuilt = await repository.ExecuteUnderSharedComponentWriteLocksAsync(componentIds, async ct =>
            {
                await PageBuilderSharedComponentReferenceIndexService.ValidateComponentsAsync(repository, componentIds, page.StoreId ?? storeId, ct);
                var references = await repository.PageBuilderSharedComponentReferences.Where(x => x.PageId == page.Id).ToListAsync(ct);
                foreach (var reference in references)
                {
                    repository.Remove(reference);
                }
                foreach (var componentId in componentIds)
                {
                    repository.Add(new PageBuilderSharedComponentReferenceEntity
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        PageId = page.Id,
                        SharedComponentId = componentId,
                    });
                }
                await repository.RebuildPageAssetReferenceIndexAsync(page.Id, ct);
                await repository.UnitOfWork.CommitAsync();
            }, cancellationToken);
            if (!rebuilt)
            {
                throw new InvalidDataException("A referenced Shared Component no longer exists.");
            }
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

            if (ids.Length > 0)
            {
                foreach (var batch in ids.Chunk(ExistingGroupsQueryBatchSize))
                {
                    var referencedGroupIds = await repository.PageBuilderPages
                        .Where(x => batch.Contains(x.GroupId))
                        .Join(
                            repository.PageBuilderSharedComponentReferences,
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

                foreach (var page in group.Pages ?? [])
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

        public virtual async Task<PageBuilderConditionalContentWriteResult> SaveGroupContentAsync(
            GroupedPageBuilderPage authorizedGroup, string content, string expectedETag,
            CancellationToken cancellationToken = default)
        {
            GroupedPageBuilderPage savedGroup = authorizedGroup;
            string pageId = null;
            var contentChanged = false;
            var groupEventsPublished = false;
            await SaveChangesInternalAsync([authorizedGroup], async (repository, entities, ct) =>
            {
                var current = GetAuthorizedGroup(authorizedGroup, entities);
                savedGroup = current;
                var snapshot = await ReadContentAsync(repository, current, draft: true, ct);
                if (!string.Equals(snapshot.ETag, expectedETag, StringComparison.Ordinal))
                {
                    // A response can be lost after commit. Retrying the identical document is harmless;
                    // a different document must still prove it was based on the current version.
                    if (!string.Equals(snapshot.Content, content, StringComparison.Ordinal))
                    {
                        throw new PageBuilderContentConflictException();
                    }
                    pageId = snapshot.PageId;
                    return null;
                }

                groupEventsPublished = !current.Pages.Any(x => x.Status == Draft);
                var target = GetOrAddDraft(current);
                target.Content = content;
                pageId = target.Id;
                contentChanged = true;
                return new PreparedSave(current, groupEventsPublished);
            }, cancellationToken);

            // The token depends only on the locked group's identity and the submitted document.
            // Hashing the next version does not need to keep database locks held.
            return new(pageId, PageBuilderContentVersion.Create(savedGroup, content))
            {
                ContentChanged = contentChanged,
                GroupEventsPublished = groupEventsPublished,
            };
        }

        public virtual async Task<PageBuilderContentSnapshot> LoadGroupContentAsync(
            GroupedPageBuilderPage authorizedGroup, bool draft = true, CancellationToken cancellationToken = default)
        {
            using var repository = _repositoryFactory();
            var entities = await repository.GetGroupedPageBuilderPagesByIdsAsync([authorizedGroup.Id], null);
            var current = GetAuthorizedGroup(authorizedGroup, entities);
            return await ReadContentAsync(repository, current, draft, cancellationToken);
        }

        public virtual Task SaveGroupSettingsAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default)
        {
            return SaveChangesInternalAsync([group], async (repository, entities, ct) =>
            {
                var existing = entities.SingleOrDefault();
                var settingsChanged = existing == null || existing.Name != group.Name
                    || existing.Permalink != group.Permalink || existing.CultureName != group.CultureName;
                if (existing != null)
                {
                    if (existing.CreatedDate != group.CreatedDate)
                    {
                        throw new KeyNotFoundException("The authorized page group no longer exists.");
                    }
                    // Membership is read under the group lock; a cached Shell model cannot hide a new draft.
                    group.Pages = ToModel(existing, null).Pages;
                }
                var snapshot = await ReadContentAsync(repository, group, draft: true, ct);
                if (!settingsChanged && snapshot.Content != null)
                {
                    return new PreparedSave(group);
                }
                var updated = SynchronizeContentSettings(snapshot.Content, group);
                if (!string.Equals(updated, snapshot.Content, StringComparison.Ordinal))
                {
                    GetOrAddDraft(group).Content = updated;
                }
                return new PreparedSave(group);
            }, cancellationToken);
        }

        private GroupedPageBuilderPage GetAuthorizedGroup(
            GroupedPageBuilderPage authorizedGroup, IList<GroupedPageBuilderPageEntity> entities)
        {
            var entity = entities.SingleOrDefault();
            if (entity == null || !string.Equals(entity.StoreId, authorizedGroup.StoreId, StringComparison.OrdinalIgnoreCase)
                || entity.CreatedDate != authorizedGroup.CreatedDate)
            {
                throw new KeyNotFoundException("The authorized page group no longer exists.");
            }
            return ToModel(entity, null);
        }

        private static async Task<PageBuilderContentSnapshot> ReadContentAsync(
            IPageBuilderModuleRepository repository, GroupedPageBuilderPage group, bool draft, CancellationToken cancellationToken)
        {
            foreach (var pageId in PageBuilderPageSelection.Order(group.Pages, draft).Select(page => page.Id))
            {
                var content = await repository.PageBuilderContents.AsNoTracking()
                    .Where(x => x.Id == pageId).Select(x => x.PageContent).FirstOrDefaultAsync(cancellationToken);
                if (content != null)
                {
                    return new(pageId, content, PageBuilderContentVersion.Create(group, content));
                }
            }
            return new(null, null, PageBuilderContentVersion.Create(group, null));
        }

        private static PageBuilderPage GetOrAddDraft(GroupedPageBuilderPage group)
        {
            var draft = PageBuilderPageSelection.Order(group.Pages).FirstOrDefault(x => x.Status == Draft);
            if (draft == null)
            {
                draft = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
                draft.Id = Guid.NewGuid().ToString("N");
                draft.GroupId = group.Id;
                draft.StoreId = group.StoreId;
                draft.Status = Draft;
                group.Pages.Add(draft);
            }
            return draft;
        }

        internal static string SynchronizeContentSettings(string content, GroupedPageBuilderPage group)
        {
            var source = string.IsNullOrWhiteSpace(content) ? Core.ModuleConstants.DefaultPageContent : content;
            if (JsonNode.Parse(source) is not JsonObject root)
            {
                return content;
            }
            var settings = root["settings"] as JsonObject;
            if (settings != null && HasStringValue(settings["name"], group.Name)
                && HasStringValue(settings["permalink"], group.Permalink) && HasStringValue(settings["cultureName"], group.CultureName))
            {
                return content;
            }
            settings ??= new JsonObject();
            root["settings"] = settings;
            settings["name"] = group.Name;
            settings["permalink"] = group.Permalink;
            settings["cultureName"] = group.CultureName;
            return root.ToJsonString();
        }

        private static bool HasStringValue(JsonNode node, string expected)
            => node == null ? expected == null : node is JsonValue value && value.TryGetValue<string>(out var text) && text == expected;

        private sealed record PreparedSave(GroupedPageBuilderPage Model, bool PublishGroupedEvents = true);

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
            await repository.SavePageContentAsync(
                pageId,
                content,
                cancellationToken);
        }

        public async Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default)
        {
            await using var repository = _contentStreamRepositoryFactory();
            await repository.CopyPageContentAsync(
                sourcePageId,
                targetPageId,
                cancellationToken);
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
                await repository.ExecuteUnderPageWriteLocksAsync(
                    [pageId],
                    async transactionCancellationToken =>
                    {
                        // A successful concurrent content writer holds this same row lock. Rechecking after the
                        // lock prevents failed-request cleanup from deleting a draft another request filled.
                        var page = await repository.PageBuilderPages
                            .FirstOrDefaultAsync(x => x.Id == pageId, transactionCancellationToken);
                        if (page == null || page.Status != Draft)
                        {
                            return;
                        }

                        var hasContent = await repository.PageBuilderContents
                            .AnyAsync(
                                x => x.Id == pageId && x.PageContent != null,
                                transactionCancellationToken);
                        var hasSharedComponentReferences = await repository.PageBuilderSharedComponentReferences
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

    }
}
