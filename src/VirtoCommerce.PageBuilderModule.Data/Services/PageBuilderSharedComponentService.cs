using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core;
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

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderSharedComponentService
    : CrudService<PageBuilderSharedComponent, PageBuilderSharedComponentEntity,
        PageBuilderSharedComponentChangingEvent, PageBuilderSharedComponentChangedEvent>,
      IPageBuilderSharedComponentService
{
    private const int ExistingComponentsQueryBatchSize = 500;

    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<PageBuilderSharedComponentService> _logger;
    private readonly Func<IPageBuilderModuleRepository> _repositoryFactory;

    public PageBuilderSharedComponentService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher,
        ILogger<PageBuilderSharedComponentService> logger = null)
        : base(repositoryFactory, platformMemoryCache, eventPublisher)
    {
        _repositoryFactory = repositoryFactory;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public override Task SaveChangesAsync(IList<PageBuilderSharedComponent> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        if (models.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (models.Any(model => string.IsNullOrWhiteSpace(model?.Id)))
        {
            throw new InvalidOperationException(
                "Shared Components must be created together with their content. Use SaveWithContentAsync.");
        }

        return SaveChangesInternalAsync(models);
    }

    private async Task SaveChangesInternalAsync(IList<PageBuilderSharedComponent> models)
    {
        if (!await SaveMetadataBatchAsync(models, CancellationToken.None))
        {
            throw new KeyNotFoundException("One or more Shared Components were not found or were replaced.");
        }
    }

    public Task<PageBuilderSharedComponent> UpdateMetadataAsync(
        PageBuilderSharedComponent model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.Id);

        return UpdateMetadataInternalAsync(model, cancellationToken);
    }

    private async Task<PageBuilderSharedComponent> UpdateMetadataInternalAsync(
        PageBuilderSharedComponent model,
        CancellationToken cancellationToken)
    {
        return await SaveMetadataBatchAsync([model], cancellationToken, validateStorePreflight: false)
            ? model
            : null;
    }

    private async Task<bool> SaveMetadataBatchAsync(
        IList<PageBuilderSharedComponent> models,
        CancellationToken cancellationToken,
        bool validateStorePreflight = true)
    {
        await PrepareMetadataBatchAsync(models, validateStorePreflight);

        var ids = PageBuilderWriteLock.OrderIds(models.Select(x => x.Id));
        if (ids.Length != models.Count)
        {
            throw new InvalidDataException("A metadata batch cannot contain duplicate Shared Component ids.");
        }

        var state = new MetadataBatchSaveState(models.Count);

        using (var repository = _repositoryFactory())
        {
            if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                repository is not IPageBuilderSharedComponentRepository sharedComponentRepository)
            {
                return false;
            }

            if (!await TryPersistMetadataBatchAsync(
                    repository,
                    writeLockRepository,
                    sharedComponentRepository,
                    models,
                    ids,
                    state,
                    cancellationToken))
            {
                return false;
            }
        }

        state.PrimaryKeyMap.ResolvePrimaryKeys();
        ClearCache(state.OriginalModels);
        ClearCache(models);

        UpdateChangedModels(state);

        await AfterSaveChangesAsync(models, state.ChangedEntries);
        await PublishAfterCommitAsync(
            EventFactory<PageBuilderSharedComponentChangedEvent>(state.ChangedEntries),
            string.Join(", ", ids));

        return true;
    }

    private async Task PrepareMetadataBatchAsync(
        IList<PageBuilderSharedComponent> models,
        bool validateStorePreflight)
    {
        if (validateStorePreflight)
        {
            await BeforeSaveChanges(models);
            return;
        }

        foreach (var model in models)
        {
            ValidateAndNormalize(model);
        }

        await base.BeforeSaveChanges(models);
    }

    private async Task<bool> TryPersistMetadataBatchAsync(
        IPageBuilderModuleRepository repository,
        IPageBuilderWriteLockRepository writeLockRepository,
        IPageBuilderSharedComponentRepository sharedComponentRepository,
        IList<PageBuilderSharedComponent> models,
        string[] ids,
        MetadataBatchSaveState state,
        CancellationToken cancellationToken)
    {
        var allComponentsExist = await writeLockRepository.ExecuteUnderSharedComponentWriteLocksAsync(
            ids,
            async transactionCancellationToken =>
            {
                var entities = await LoadSharedComponentEntitiesAsync(
                    sharedComponentRepository,
                    ids,
                    transactionCancellationToken);
                var entitiesById = entities.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
                var contentIds = await LoadSharedComponentContentIdsAsync(
                    sharedComponentRepository,
                    ids,
                    transactionCancellationToken);

                if (!AllAggregatesMatch(models, entitiesById, contentIds))
                {
                    return;
                }

                TrackMetadataChanges(models, entitiesById, state);
                await _eventPublisher.Publish(
                    EventFactory<PageBuilderSharedComponentChangingEvent>(state.ChangedEntries),
                    transactionCancellationToken);
                await repository.UnitOfWork.CommitAsync();
                state.Saved = true;
            },
            cancellationToken);

        return allComponentsExist && state.Saved;
    }

    private static bool AllAggregatesMatch(
        IEnumerable<PageBuilderSharedComponent> models,
        Dictionary<string, PageBuilderSharedComponentEntity> entitiesById,
        HashSet<string> contentIds)
    {
        return models.All(model =>
            entitiesById.TryGetValue(model.Id, out var entity) &&
            contentIds.Contains(model.Id) &&
            HasExpectedIdentity(entity, model));
    }

    private void TrackMetadataChanges(
        IEnumerable<PageBuilderSharedComponent> models,
        Dictionary<string, PageBuilderSharedComponentEntity> entitiesById,
        MetadataBatchSaveState state)
    {
        foreach (var model in models)
        {
            var originalEntity = entitiesById[model.Id];
            var originalModel = ToModel(originalEntity, model: null);
            var modifiedEntity = FromModel(model, state.PrimaryKeyMap);

            state.OriginalModels.Add(originalModel);
            state.ChangedEntries.Add(new GenericChangedEntry<PageBuilderSharedComponent>(
                model,
                originalModel,
                EntryState.Modified));
            modifiedEntity.Patch(originalEntity);
            originalEntity.ModifiedDate = DateTime.UtcNow;
            state.ChangedEntities.Add(originalEntity);
        }
    }

    private void UpdateChangedModels(MetadataBatchSaveState state)
    {
        for (var index = 0; index < state.ChangedEntries.Count; index++)
        {
            var changedEntry = state.ChangedEntries[index];
            changedEntry.NewEntry = ToModel(state.ChangedEntities[index], changedEntry.NewEntry);
        }
    }

    public Task<bool> TryDeleteAsync(
        PageBuilderSharedComponent expectedComponent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedComponent);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedComponent.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedComponent.StoreId);

        return TryDeleteInternalAsync(expectedComponent, cancellationToken);
    }

    private async Task<bool> TryDeleteInternalAsync(
        PageBuilderSharedComponent expectedComponent,
        CancellationToken cancellationToken)
    {
        PageBuilderSharedComponent deletedModel = null;
        GenericChangedEntry<PageBuilderSharedComponent> changedEntry = null;

        using (var repository = _repositoryFactory())
        {
            if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                repository is not IPageBuilderSharedComponentRepository sharedComponentRepository)
            {
                return false;
            }

            var componentExists = await writeLockRepository.ExecuteUnderSharedComponentWriteLockAsync(
                expectedComponent.Id,
                async transactionCancellationToken =>
                {
                    var entity = await sharedComponentRepository.PageBuilderSharedComponents
                        .FirstAsync(x => x.Id == expectedComponent.Id, transactionCancellationToken);
                    var contentExists = await sharedComponentRepository.PageBuilderSharedComponentContents
                        .AnyAsync(x => x.Id == expectedComponent.Id, transactionCancellationToken);

                    if (!contentExists ||
                        !string.Equals(entity.StoreId, expectedComponent.StoreId, StringComparison.OrdinalIgnoreCase) ||
                        entity.CreatedDate != expectedComponent.CreatedDate)
                    {
                        return;
                    }

                    deletedModel = ToModel(entity, model: null);
                    changedEntry = new GenericChangedEntry<PageBuilderSharedComponent>(
                        deletedModel,
                        EntryState.Deleted);
                    await _eventPublisher.Publish(
                        EventFactory<PageBuilderSharedComponentChangingEvent>([changedEntry]),
                        transactionCancellationToken);

                    repository.Remove(entity);
                    await repository.UnitOfWork.CommitAsync();
                },
                cancellationToken);

            if (!componentExists || deletedModel == null)
            {
                return false;
            }
        }

        ClearCache([deletedModel]);
        await AfterDeleteAsync([deletedModel], [changedEntry]);
        await PublishAfterCommitAsync(
            EventFactory<PageBuilderSharedComponentChangedEvent>([changedEntry]),
            deletedModel.Id);

        return true;
    }

    public Task SaveWithContentAsync(
        PageBuilderSharedComponent model,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        PageBuilderSharedComponentReferenceMatcher.ValidateComponentContent(content);

        return SaveWithContentInternalAsync(model, content, cancellationToken);
    }

    private async Task SaveWithContentInternalAsync(
        PageBuilderSharedComponent model,
        string content,
        CancellationToken cancellationToken)
    {
        var state = new SharedComponentSaveState(model, content);
        await BeforeSaveChanges(state.Models);

        using (var repository = _repositoryFactory())
        {
            if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                repository is not IPageBuilderSharedComponentRepository sharedComponentRepository)
            {
                throw new NotSupportedException("Shared Component writes require repository write-lock support.");
            }

            await PersistAggregateUnderWriteLockAsync(
                sharedComponentRepository,
                writeLockRepository,
                state,
                cancellationToken);
        }

        state.PrimaryKeyMap.ResolvePrimaryKeys();
        ClearCache(state.OriginalModels);
        ClearCache(state.Models);

        GenericSearchCachingRegion<PageBuilderPage>.ExpireRegion();

        state.ChangedEntry.NewEntry = ToModel(state.ChangedEntity, state.ChangedEntry.NewEntry);
        await AfterSaveChangesAsync(state.Models, [state.ChangedEntry]);

        await PublishChangedEventsAsync(state);
    }

    private async Task PersistAggregateUnderWriteLockAsync(
        IPageBuilderSharedComponentRepository repository,
        IPageBuilderWriteLockRepository writeLockRepository,
        SharedComponentSaveState state,
        CancellationToken cancellationToken)
    {
        var existingComponentSaved = false;
        var existingComponentMatched = false;
        if (!string.IsNullOrWhiteSpace(state.Model.Id))
        {
            existingComponentSaved = await writeLockRepository.ExecuteUnderSharedComponentWriteLockAsync(
                state.Model.Id,
                async transactionCancellationToken =>
                {
                    var existingEntities = await LoadExistingEntities(repository, state.Models);
                    var originalEntity = FindExistingEntity(existingEntities, state.Model);
                    if (!HasExpectedIdentity(originalEntity, state.Model))
                    {
                        return;
                    }

                    existingComponentMatched = true;
                    await PersistAggregateAsync(repository, originalEntity, state, transactionCancellationToken);
                },
                cancellationToken);
        }

        if (existingComponentSaved && !existingComponentMatched)
        {
            throw new DbUpdateConcurrencyException(
                $"Shared Component '{state.Model.Id}' was replaced while it was being saved.");
        }

        if (!existingComponentSaved)
        {
            if (state.Model.CreatedDate != default)
            {
                throw new DbUpdateConcurrencyException(
                    $"Shared Component '{state.Model.Id}' was deleted while it was being saved.");
            }

            await PersistAggregateAsync(repository, originalEntity: null, state, cancellationToken);
        }
    }

    private async Task PersistAggregateAsync(
        IPageBuilderSharedComponentRepository repository,
        PageBuilderSharedComponentEntity originalEntity,
        SharedComponentSaveState state,
        CancellationToken cancellationToken)
    {
        var changedEntity = TrackChangedEntity(repository, originalEntity, state);
        await UpsertContentAsync(repository, changedEntity.Id, state.Content, cancellationToken);

        await PageBuilderSharedComponentAssetReferenceIndexService.RebuildIndexInCurrentUnitOfWorkAsync(
            repository,
            changedEntity.Id,
            state.Content,
            cancellationToken);
        var changingEvent = EventFactory<PageBuilderSharedComponentChangingEvent>([state.ChangedEntry]);
        await _eventPublisher.Publish(changingEvent, cancellationToken);
        await repository.UnitOfWork.CommitAsync();
    }

    private PageBuilderSharedComponentEntity TrackChangedEntity(
        IPageBuilderModuleRepository repository,
        PageBuilderSharedComponentEntity originalEntity,
        SharedComponentSaveState state)
    {
        var modifiedEntity = FromModel(state.Model, state.PrimaryKeyMap);
        if (originalEntity == null)
        {
            // The platform normally generates string keys when an entity is tracked. Assigning the same
            // compact GUID explicitly lets the required content child and its asset references share the
            // aggregate key before the single database commit.
            modifiedEntity.Id ??= Guid.NewGuid().ToString("N");
            repository.Add(modifiedEntity);
            state.ChangedEntity = modifiedEntity;
            state.ChangedEntry = new GenericChangedEntry<PageBuilderSharedComponent>(state.Model, EntryState.Added);
        }
        else
        {
            var originalModel = ToModel(originalEntity, model: null);
            state.OriginalModels.Add(originalModel);
            state.ChangedEntry = new GenericChangedEntry<PageBuilderSharedComponent>(state.Model, originalModel, EntryState.Modified);
            modifiedEntity.Patch(originalEntity);
            originalEntity.ModifiedDate = DateTime.UtcNow;
            state.ChangedEntity = originalEntity;
        }

        return state.ChangedEntity;
    }

    private static async Task UpsertContentAsync(
        IPageBuilderSharedComponentRepository repository,
        string componentId,
        string content,
        CancellationToken cancellationToken)
    {
        var contentEntity = await repository.PageBuilderSharedComponentContents
            .FirstOrDefaultAsync(x => x.Id == componentId, cancellationToken);

        if (contentEntity == null)
        {
            repository.Add(new PageBuilderSharedComponentContentEntity
            {
                Id = componentId,
                ComponentContent = content,
            });
        }
        else
        {
            contentEntity.ComponentContent = content;
            repository.Update(contentEntity);
        }
    }

    private async Task PublishChangedEventsAsync(SharedComponentSaveState state)
    {
        var changedEvent = EventFactory<PageBuilderSharedComponentChangedEvent>([state.ChangedEntry]);
        await PublishAfterCommitAsync(changedEvent, state.Model.Id);
        await PublishAfterCommitAsync(
            new PageBuilderSharedComponentContentChangedEvent([state.Model.Id]),
            state.Model.Id);
    }

    private async Task PublishAfterCommitAsync<TEvent>(TEvent @event, string sharedComponentId)
        where TEvent : IEvent
    {
        try
        {
            await _eventPublisher.Publish(@event, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to publish post-commit {EventType} for Shared Component {SharedComponentId}",
                typeof(TEvent).Name,
                sharedComponentId);
        }
    }

    protected override Task<IList<PageBuilderSharedComponentEntity>> LoadEntities(
        IRepository repository,
        IList<string> ids,
        string responseGroup)
    {
        return ((IPageBuilderModuleRepository)repository)
            .RequireSharedComponents()
            .GetPageBuilderSharedComponentsByIdsAsync(ids, responseGroup);
    }

    protected override async Task BeforeSaveChanges(IList<PageBuilderSharedComponent> models)
    {
        foreach (var model in models)
        {
            ValidateAndNormalize(model);
        }

        var ids = models
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => x.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingStores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (ids.Length > 0)
        {
            using var repository = _repositoryFactory();
            var sharedComponentRepository = repository.RequireSharedComponents();
            foreach (var batch in ids.Chunk(ExistingComponentsQueryBatchSize))
            {
                var existing = await sharedComponentRepository.PageBuilderSharedComponents
                    .Where(x => batch.Contains(x.Id))
                    .Select(x => new { x.Id, x.StoreId })
                    .ToListAsync();

                foreach (var item in existing)
                {
                    existingStores[item.Id] = item.StoreId;
                }
            }
        }

        ValidateStoreImmutability(models, existingStores);
        await base.BeforeSaveChanges(models);
    }

    internal static void ValidateAndNormalize(PageBuilderSharedComponent model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.StoreId);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.Name);

        model.Name = model.Name.Trim();
        if (model.Name.Length > ModuleConstants.SharedComponents.NameMaxLength)
        {
            throw new InvalidDataException(
                $"Shared Component name cannot exceed {ModuleConstants.SharedComponents.NameMaxLength} characters.");
        }
    }

    internal static void ValidateStoreImmutability(
        IEnumerable<PageBuilderSharedComponent> models,
        IReadOnlyDictionary<string, string> existingStores)
    {
        foreach (var model in models.Where(x => !string.IsNullOrWhiteSpace(x.Id)))
        {
            if (existingStores.TryGetValue(model.Id, out var existingStoreId) &&
                !string.Equals(existingStoreId, model.StoreId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Shared Component '{model.Id}' cannot be moved from store '{existingStoreId}' to '{model.StoreId}'.");
            }
        }
    }

    private static bool HasExpectedIdentity(
        PageBuilderSharedComponentEntity entity,
        PageBuilderSharedComponent model)
    {
        return entity != null &&
               string.Equals(entity.StoreId, model.StoreId, StringComparison.OrdinalIgnoreCase) &&
               (model.CreatedDate == default || entity.CreatedDate == model.CreatedDate);
    }

    private static async Task<List<PageBuilderSharedComponentEntity>> LoadSharedComponentEntitiesAsync(
        IPageBuilderSharedComponentRepository repository,
        IEnumerable<string> ids,
        CancellationToken cancellationToken)
    {
        var result = new List<PageBuilderSharedComponentEntity>();
        foreach (var batch in ids.Chunk(ExistingComponentsQueryBatchSize))
        {
            result.AddRange(await repository.PageBuilderSharedComponents
                .Where(x => batch.Contains(x.Id))
                .ToListAsync(cancellationToken));
        }

        return result;
    }

    private static async Task<HashSet<string>> LoadSharedComponentContentIdsAsync(
        IPageBuilderSharedComponentRepository repository,
        IEnumerable<string> ids,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in ids.Chunk(ExistingComponentsQueryBatchSize))
        {
            result.UnionWith(await repository.PageBuilderSharedComponentContents
                .Where(x => batch.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken));
        }

        return result;
    }

    private sealed class SharedComponentSaveState
    {
        public SharedComponentSaveState(PageBuilderSharedComponent model, string content)
        {
            Model = model;
            Content = content;
            Models = [model];
        }

        public PageBuilderSharedComponent Model { get; }
        public string Content { get; }
        public List<PageBuilderSharedComponent> Models { get; }
        public List<PageBuilderSharedComponent> OriginalModels { get; } = [];
        public PrimaryKeyResolvingMap PrimaryKeyMap { get; } = new();
        public PageBuilderSharedComponentEntity ChangedEntity { get; set; }
        public GenericChangedEntry<PageBuilderSharedComponent> ChangedEntry { get; set; }
    }

    private sealed class MetadataBatchSaveState(int capacity)
    {
        public List<PageBuilderSharedComponent> OriginalModels { get; } = new(capacity);
        public List<GenericChangedEntry<PageBuilderSharedComponent>> ChangedEntries { get; } = new(capacity);
        public List<PageBuilderSharedComponentEntity> ChangedEntities { get; } = new(capacity);
        public PrimaryKeyResolvingMap PrimaryKeyMap { get; } = new();
        public bool Saved { get; set; }
    }
}
