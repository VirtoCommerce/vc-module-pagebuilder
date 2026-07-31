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

public class PageBuilderLinkedComponentService
    : CrudService<PageBuilderLinkedComponent, PageBuilderLinkedComponentEntity,
        PageBuilderLinkedComponentChangingEvent, PageBuilderLinkedComponentChangedEvent>,
      IPageBuilderLinkedComponentService
{
    private const int ExistingComponentsQueryBatchSize = 500;

    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<PageBuilderLinkedComponentService> _logger;
    private readonly Func<IPageBuilderModuleRepository> _repositoryFactory;

    public PageBuilderLinkedComponentService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher,
        ILogger<PageBuilderLinkedComponentService> logger = null)
        : base(repositoryFactory, platformMemoryCache, eventPublisher)
    {
        _repositoryFactory = repositoryFactory;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public override Task SaveChangesAsync(IList<PageBuilderLinkedComponent> models)
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

    private async Task SaveChangesInternalAsync(IList<PageBuilderLinkedComponent> models)
    {
        if (!await SaveMetadataBatchAsync(models, CancellationToken.None))
        {
            throw new KeyNotFoundException("One or more Linked Components were not found or were replaced.");
        }
    }

    public Task<PageBuilderLinkedComponent> UpdateMetadataAsync(
        PageBuilderLinkedComponent model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.Id);

        return UpdateMetadataInternalAsync(model, cancellationToken);
    }

    private async Task<PageBuilderLinkedComponent> UpdateMetadataInternalAsync(
        PageBuilderLinkedComponent model,
        CancellationToken cancellationToken)
    {
        return await SaveMetadataBatchAsync([model], cancellationToken, validateStorePreflight: false)
            ? model
            : null;
    }

    private async Task<bool> SaveMetadataBatchAsync(
        IList<PageBuilderLinkedComponent> models,
        CancellationToken cancellationToken,
        bool validateStorePreflight = true)
    {
        await PrepareMetadataBatchAsync(models, validateStorePreflight);

        var ids = PageBuilderWriteLock.OrderIds(models.Select(x => x.Id));
        if (ids.Length != models.Count)
        {
            throw new InvalidDataException("A metadata batch cannot contain duplicate Linked Component ids.");
        }

        var state = new MetadataBatchSaveState(models.Count);

        using (var repository = _repositoryFactory())
        {
            if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                repository is not IPageBuilderLinkedComponentRepository linkedRepository)
            {
                return false;
            }

            if (!await TryPersistMetadataBatchAsync(
                    repository,
                    writeLockRepository,
                    linkedRepository,
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
            EventFactory<PageBuilderLinkedComponentChangedEvent>(state.ChangedEntries),
            string.Join(", ", ids));

        return true;
    }

    private async Task PrepareMetadataBatchAsync(
        IList<PageBuilderLinkedComponent> models,
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
        IPageBuilderLinkedComponentRepository linkedRepository,
        IList<PageBuilderLinkedComponent> models,
        string[] ids,
        MetadataBatchSaveState state,
        CancellationToken cancellationToken)
    {
        var allComponentsExist = await writeLockRepository.ExecuteUnderLinkedComponentWriteLocksAsync(
            ids,
            async transactionCancellationToken =>
            {
                var entities = await LoadLinkedComponentEntitiesAsync(
                    linkedRepository,
                    ids,
                    transactionCancellationToken);
                var entitiesById = entities.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
                var contentIds = await LoadLinkedComponentContentIdsAsync(
                    linkedRepository,
                    ids,
                    transactionCancellationToken);

                if (!AllAggregatesMatch(models, entitiesById, contentIds))
                {
                    return;
                }

                TrackMetadataChanges(models, entitiesById, state);
                await _eventPublisher.Publish(
                    EventFactory<PageBuilderLinkedComponentChangingEvent>(state.ChangedEntries),
                    transactionCancellationToken);
                await repository.UnitOfWork.CommitAsync();
                state.Saved = true;
            },
            cancellationToken);

        return allComponentsExist && state.Saved;
    }

    private static bool AllAggregatesMatch(
        IEnumerable<PageBuilderLinkedComponent> models,
        Dictionary<string, PageBuilderLinkedComponentEntity> entitiesById,
        HashSet<string> contentIds)
    {
        return models.All(model =>
            entitiesById.TryGetValue(model.Id, out var entity) &&
            contentIds.Contains(model.Id) &&
            HasExpectedIdentity(entity, model));
    }

    private void TrackMetadataChanges(
        IEnumerable<PageBuilderLinkedComponent> models,
        Dictionary<string, PageBuilderLinkedComponentEntity> entitiesById,
        MetadataBatchSaveState state)
    {
        foreach (var model in models)
        {
            var originalEntity = entitiesById[model.Id];
            var originalModel = ToModel(originalEntity, model: null);
            var modifiedEntity = FromModel(model, state.PrimaryKeyMap);

            state.OriginalModels.Add(originalModel);
            state.ChangedEntries.Add(new GenericChangedEntry<PageBuilderLinkedComponent>(
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
        PageBuilderLinkedComponent expectedComponent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedComponent);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedComponent.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedComponent.StoreId);

        return TryDeleteInternalAsync(expectedComponent, cancellationToken);
    }

    private async Task<bool> TryDeleteInternalAsync(
        PageBuilderLinkedComponent expectedComponent,
        CancellationToken cancellationToken)
    {
        PageBuilderLinkedComponent deletedModel = null;
        GenericChangedEntry<PageBuilderLinkedComponent> changedEntry = null;

        using (var repository = _repositoryFactory())
        {
            if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                repository is not IPageBuilderLinkedComponentRepository linkedRepository)
            {
                return false;
            }

            var componentExists = await writeLockRepository.ExecuteUnderLinkedComponentWriteLockAsync(
                expectedComponent.Id,
                async transactionCancellationToken =>
                {
                    var entity = await linkedRepository.PageBuilderLinkedComponents
                        .FirstAsync(x => x.Id == expectedComponent.Id, transactionCancellationToken);
                    var contentExists = await linkedRepository.PageBuilderLinkedComponentContents
                        .AnyAsync(x => x.Id == expectedComponent.Id, transactionCancellationToken);

                    if (!contentExists ||
                        !string.Equals(entity.StoreId, expectedComponent.StoreId, StringComparison.OrdinalIgnoreCase) ||
                        entity.CreatedDate != expectedComponent.CreatedDate)
                    {
                        return;
                    }

                    deletedModel = ToModel(entity, model: null);
                    changedEntry = new GenericChangedEntry<PageBuilderLinkedComponent>(
                        deletedModel,
                        EntryState.Deleted);
                    await _eventPublisher.Publish(
                        EventFactory<PageBuilderLinkedComponentChangingEvent>([changedEntry]),
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
            EventFactory<PageBuilderLinkedComponentChangedEvent>([changedEntry]),
            deletedModel.Id);

        return true;
    }

    public Task SaveWithContentAsync(
        PageBuilderLinkedComponent model,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(content);

        return SaveWithContentInternalAsync(model, content, cancellationToken);
    }

    private async Task SaveWithContentInternalAsync(
        PageBuilderLinkedComponent model,
        string content,
        CancellationToken cancellationToken)
    {
        var state = new LinkedComponentSaveState(model, content);
        await BeforeSaveChanges(state.Models);

        using (var repository = _repositoryFactory())
        {
            if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                repository is not IPageBuilderLinkedComponentRepository linkedRepository)
            {
                throw new NotSupportedException("Shared Component writes require repository write-lock support.");
            }

            await PersistAggregateUnderWriteLockAsync(
                linkedRepository,
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
        IPageBuilderLinkedComponentRepository repository,
        IPageBuilderWriteLockRepository writeLockRepository,
        LinkedComponentSaveState state,
        CancellationToken cancellationToken)
    {
        var existingComponentSaved = false;
        var existingComponentMatched = false;
        if (!string.IsNullOrWhiteSpace(state.Model.Id))
        {
            existingComponentSaved = await writeLockRepository.ExecuteUnderLinkedComponentWriteLockAsync(
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
                $"Linked Component '{state.Model.Id}' was replaced while it was being saved.");
        }

        if (!existingComponentSaved)
        {
            if (state.Model.CreatedDate != default)
            {
                throw new DbUpdateConcurrencyException(
                    $"Linked Component '{state.Model.Id}' was deleted while it was being saved.");
            }

            await PersistAggregateAsync(repository, originalEntity: null, state, cancellationToken);
        }
    }

    private async Task PersistAggregateAsync(
        IPageBuilderLinkedComponentRepository repository,
        PageBuilderLinkedComponentEntity originalEntity,
        LinkedComponentSaveState state,
        CancellationToken cancellationToken)
    {
        var changedEntity = TrackChangedEntity(repository, originalEntity, state);
        await UpsertContentAsync(repository, changedEntity.Id, state.Content, cancellationToken);

        await PageBuilderLinkedComponentAssetReferenceIndexService.RebuildIndexInCurrentUnitOfWorkAsync(
            repository,
            changedEntity.Id,
            state.Content,
            cancellationToken);
        var changingEvent = EventFactory<PageBuilderLinkedComponentChangingEvent>([state.ChangedEntry]);
        await _eventPublisher.Publish(changingEvent, cancellationToken);
        await repository.UnitOfWork.CommitAsync();
    }

    private PageBuilderLinkedComponentEntity TrackChangedEntity(
        IPageBuilderModuleRepository repository,
        PageBuilderLinkedComponentEntity originalEntity,
        LinkedComponentSaveState state)
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
            state.ChangedEntry = new GenericChangedEntry<PageBuilderLinkedComponent>(state.Model, EntryState.Added);
        }
        else
        {
            var originalModel = ToModel(originalEntity, model: null);
            state.OriginalModels.Add(originalModel);
            state.ChangedEntry = new GenericChangedEntry<PageBuilderLinkedComponent>(state.Model, originalModel, EntryState.Modified);
            modifiedEntity.Patch(originalEntity);
            originalEntity.ModifiedDate = DateTime.UtcNow;
            state.ChangedEntity = originalEntity;
        }

        return state.ChangedEntity;
    }

    private static async Task UpsertContentAsync(
        IPageBuilderLinkedComponentRepository repository,
        string componentId,
        string content,
        CancellationToken cancellationToken)
    {
        var contentEntity = await repository.PageBuilderLinkedComponentContents
            .FirstOrDefaultAsync(x => x.Id == componentId, cancellationToken);

        if (contentEntity == null)
        {
            repository.Add(new PageBuilderLinkedComponentContentEntity
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

    private async Task PublishChangedEventsAsync(LinkedComponentSaveState state)
    {
        var changedEvent = EventFactory<PageBuilderLinkedComponentChangedEvent>([state.ChangedEntry]);
        await PublishAfterCommitAsync(changedEvent, state.Model.Id);
        await PublishAfterCommitAsync(
            new PageBuilderLinkedComponentContentChangedEvent([state.Model.Id]),
            state.Model.Id);
    }

    private async Task PublishAfterCommitAsync<TEvent>(TEvent @event, string linkedComponentId)
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
                "Failed to publish post-commit {EventType} for Shared Component {LinkedComponentId}",
                typeof(TEvent).Name,
                linkedComponentId);
        }
    }

    protected override Task<IList<PageBuilderLinkedComponentEntity>> LoadEntities(
        IRepository repository,
        IList<string> ids,
        string responseGroup)
    {
        return ((IPageBuilderModuleRepository)repository)
            .RequireLinkedComponents()
            .GetPageBuilderLinkedComponentsByIdsAsync(ids, responseGroup);
    }

    protected override async Task BeforeSaveChanges(IList<PageBuilderLinkedComponent> models)
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
            var linkedRepository = repository.RequireLinkedComponents();
            foreach (var batch in ids.Chunk(ExistingComponentsQueryBatchSize))
            {
                var existing = await linkedRepository.PageBuilderLinkedComponents
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

    internal static void ValidateAndNormalize(PageBuilderLinkedComponent model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.StoreId);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.Name);

        model.Name = model.Name.Trim();
        if (model.Name.Length > ModuleConstants.LinkedComponents.NameMaxLength)
        {
            throw new InvalidDataException(
                $"Linked Component name cannot exceed {ModuleConstants.LinkedComponents.NameMaxLength} characters.");
        }
    }

    internal static void ValidateStoreImmutability(
        IEnumerable<PageBuilderLinkedComponent> models,
        IReadOnlyDictionary<string, string> existingStores)
    {
        foreach (var model in models.Where(x => !string.IsNullOrWhiteSpace(x.Id)))
        {
            if (existingStores.TryGetValue(model.Id, out var existingStoreId) &&
                !string.Equals(existingStoreId, model.StoreId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Linked Component '{model.Id}' cannot be moved from store '{existingStoreId}' to '{model.StoreId}'.");
            }
        }
    }

    private static bool HasExpectedIdentity(
        PageBuilderLinkedComponentEntity entity,
        PageBuilderLinkedComponent model)
    {
        return entity != null &&
               string.Equals(entity.StoreId, model.StoreId, StringComparison.OrdinalIgnoreCase) &&
               (model.CreatedDate == default || entity.CreatedDate == model.CreatedDate);
    }

    private static async Task<List<PageBuilderLinkedComponentEntity>> LoadLinkedComponentEntitiesAsync(
        IPageBuilderLinkedComponentRepository repository,
        IEnumerable<string> ids,
        CancellationToken cancellationToken)
    {
        var result = new List<PageBuilderLinkedComponentEntity>();
        foreach (var batch in ids.Chunk(ExistingComponentsQueryBatchSize))
        {
            result.AddRange(await repository.PageBuilderLinkedComponents
                .Where(x => batch.Contains(x.Id))
                .ToListAsync(cancellationToken));
        }

        return result;
    }

    private static async Task<HashSet<string>> LoadLinkedComponentContentIdsAsync(
        IPageBuilderLinkedComponentRepository repository,
        IEnumerable<string> ids,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in ids.Chunk(ExistingComponentsQueryBatchSize))
        {
            result.UnionWith(await repository.PageBuilderLinkedComponentContents
                .Where(x => batch.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken));
        }

        return result;
    }

    private sealed class LinkedComponentSaveState
    {
        public LinkedComponentSaveState(PageBuilderLinkedComponent model, string content)
        {
            Model = model;
            Content = content;
            Models = [model];
        }

        public PageBuilderLinkedComponent Model { get; }
        public string Content { get; }
        public List<PageBuilderLinkedComponent> Models { get; }
        public List<PageBuilderLinkedComponent> OriginalModels { get; } = [];
        public PrimaryKeyResolvingMap PrimaryKeyMap { get; } = new();
        public PageBuilderLinkedComponentEntity ChangedEntity { get; set; }
        public GenericChangedEntry<PageBuilderLinkedComponent> ChangedEntry { get; set; }
    }

    private sealed class MetadataBatchSaveState(int capacity)
    {
        public List<PageBuilderLinkedComponent> OriginalModels { get; } = new(capacity);
        public List<GenericChangedEntry<PageBuilderLinkedComponent>> ChangedEntries { get; } = new(capacity);
        public List<PageBuilderLinkedComponentEntity> ChangedEntities { get; } = new(capacity);
        public PrimaryKeyResolvingMap PrimaryKeyMap { get; } = new();
        public bool Saved { get; set; }
    }
}
