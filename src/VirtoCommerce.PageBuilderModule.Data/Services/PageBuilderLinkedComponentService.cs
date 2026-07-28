using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
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
    private readonly Func<IPageBuilderModuleRepository> _repositoryFactory;

    public PageBuilderLinkedComponentService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher)
        : base(repositoryFactory, platformMemoryCache, eventPublisher)
    {
        _repositoryFactory = repositoryFactory;
        _eventPublisher = eventPublisher;
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
            await PersistAggregateUnderWriteLockAsync(repository, state, cancellationToken);
        }

        state.PrimaryKeyMap.ResolvePrimaryKeys();
        ClearCache(state.OriginalModels);
        ClearCache(state.Models);

        state.ChangedEntry.NewEntry = ToModel(state.ChangedEntity, state.ChangedEntry.NewEntry);
        await AfterSaveChangesAsync(state.Models, [state.ChangedEntry]);

        await PublishChangedEventsAsync(state, cancellationToken);
    }

    private async Task PersistAggregateUnderWriteLockAsync(
        IPageBuilderModuleRepository repository,
        LinkedComponentSaveState state,
        CancellationToken cancellationToken)
    {
        var existingComponentSaved = false;
        if (!string.IsNullOrWhiteSpace(state.Model.Id))
        {
            existingComponentSaved = await repository.ExecuteUnderLinkedComponentWriteLockAsync(
                state.Model.Id,
                async transactionCancellationToken =>
                {
                    var existingEntities = await LoadExistingEntities(repository, state.Models);
                    var originalEntity = FindExistingEntity(existingEntities, state.Model);
                    await PersistAggregateAsync(repository, originalEntity, state, transactionCancellationToken);
                },
                cancellationToken);
        }

        if (!existingComponentSaved)
        {
            await PersistAggregateAsync(repository, originalEntity: null, state, cancellationToken);
        }
    }

    private async Task PersistAggregateAsync(
        IPageBuilderModuleRepository repository,
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
        IPageBuilderModuleRepository repository,
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

    private async Task PublishChangedEventsAsync(
        LinkedComponentSaveState state,
        CancellationToken cancellationToken)
    {
        var changedEvent = EventFactory<PageBuilderLinkedComponentChangedEvent>([state.ChangedEntry]);
        await _eventPublisher.Publish(changedEvent, cancellationToken);

        await _eventPublisher.Publish(
            new PageBuilderLinkedComponentContentChangedEvent([state.Model.Id]),
            cancellationToken);
    }

    protected override Task<IList<PageBuilderLinkedComponentEntity>> LoadEntities(
        IRepository repository,
        IList<string> ids,
        string responseGroup)
    {
        return ((IPageBuilderModuleRepository)repository)
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
            foreach (var batch in ids.Chunk(ExistingComponentsQueryBatchSize))
            {
                var existing = await repository.PageBuilderLinkedComponents
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
}
