using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class MultilanguagePageService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher,
    IGroupedPageService groupedPageService,
    IGroupedPageContentService groupedPageContentService,
    IGroupedPageSearchService groupedPageSearchService,
    IStoreService storeService)
    : CrudService<MultilanguagePage, MultilanguagePageEntity, MultilanguagePageChangingEvent, MultilanguagePageChangedEvent>(
        repositoryFactory, platformMemoryCache, eventPublisher),
      IMultilanguagePageService
{
    protected override Task<IList<MultilanguagePageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IPageBuilderModuleRepository)repository).GetMultilanguagePagesByIdsAsync(ids, responseGroup);
    }

    protected override async Task BeforeSaveChanges(IList<MultilanguagePage> models)
    {
        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.StoreId))
            {
                throw new InvalidOperationException("Multilanguage page must belong to a store.");
            }

            if (string.IsNullOrWhiteSpace(model.DefaultCultureName))
            {
                var store = await storeService.GetByIdAsync(model.StoreId)
                    ?? throw new InvalidOperationException($"Store '{model.StoreId}' not found.");
                model.DefaultCultureName = store.DefaultLanguage;
            }
        }

        await base.BeforeSaveChanges(models);
    }

    protected override async Task AfterSaveChangesAsync(IList<MultilanguagePage> models, IList<GenericChangedEntry<MultilanguagePage>> changedEntries)
    {
        await base.AfterSaveChangesAsync(models, changedEntries);

        foreach (var root in changedEntries.Where(x => x.EntryState == EntryState.Modified).Select(x => x.NewEntry))
        {
            foreach (var group in await GetLanguagesAsync(root.Id))
            {
                if (MultilanguagePageInheritance.Apply(root, group))
                {
                    await groupedPageContentService.SaveGroupSettingsAsync(group);
                }
            }
        }
    }

    public override async Task DeleteAsync(IList<string> ids, bool softDelete = false)
    {
        foreach (var id in ids)
        {
            foreach (var group in await GetLanguagesAsync(id))
            {
                await UnlinkAsync(group.Id);
            }
        }

        await base.DeleteAsync(ids, softDelete);
    }

    public virtual async Task<GroupedPageBuilderPage> LinkAsync(string multilanguagePageId, string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(multilanguagePageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        var root = await this.GetByIdAsync(multilanguagePageId)
            ?? throw new KeyNotFoundException($"Multilanguage page '{multilanguagePageId}' not found.");
        var group = await groupedPageService.GetByIdAsync(groupId)
            ?? throw new KeyNotFoundException($"Page '{groupId}' not found.");

        if (group.MultilanguagePageId == root.Id)
        {
            return group;
        }

        if (!string.IsNullOrEmpty(group.MultilanguagePageId))
        {
            throw new InvalidOperationException($"Page '{groupId}' is already a language version of '{group.MultilanguagePageId}'.");
        }

        if (!string.Equals(group.StoreId, root.StoreId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Page '{groupId}' belongs to another store.");
        }

        var languages = await GetLanguagesAsync(root.Id);
        if (languages.Any(x => string.Equals(x.CultureName, group.CultureName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Multilanguage page '{root.Id}' already has a '{group.CultureName}' version.");
        }

        await SetMultilanguagePageIdAsync(groupId, root.Id, cancellationToken);

        group = await groupedPageService.GetByIdAsync(groupId);
        // Whatever differs from the root stays as it is, so the page keeps serving the same permalink and settings.
        MultilanguagePageInheritance.SetOverriddenFields(group, MultilanguagePageInheritance.GetDifferingFields(root, group));
        MultilanguagePageInheritance.Apply(root, group);
        await groupedPageContentService.SaveGroupSettingsAsync(group, cancellationToken);

        return group;
    }

    public virtual async Task<GroupedPageBuilderPage> UnlinkAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        var group = await groupedPageService.GetByIdAsync(groupId)
            ?? throw new KeyNotFoundException($"Page '{groupId}' not found.");

        if (string.IsNullOrEmpty(group.MultilanguagePageId))
        {
            return group;
        }

        await SetMultilanguagePageIdAsync(groupId, null, cancellationToken);

        group = await groupedPageService.GetByIdAsync(groupId);
        // The page already stores effective values, so it keeps serving exactly what it served as a language version.
        MultilanguagePageInheritance.SetOverriddenFields(group, []);
        await groupedPageContentService.SaveGroupSettingsAsync(group, cancellationToken);

        return group;
    }

    protected virtual async Task<IList<GroupedPageBuilderPage>> GetLanguagesAsync(string multilanguagePageId)
    {
        var criteria = AbstractTypeFactory<PageBuilderPageSearchCriteria>.TryCreateInstance();
        criteria.MultilanguagePageId = multilanguagePageId;

        return await groupedPageSearchService.SearchAllAsync(criteria);
    }

    private async Task SetMultilanguagePageIdAsync(string groupId, string multilanguagePageId, CancellationToken cancellationToken)
    {
        // A regular grouped save never patches the link (see GroupedPageBuilderPageEntity.Patch), so it is written here,
        // under the same lock as other grouped writers.
        using (var repository = repositoryFactory())
        {
            await repository.ExecuteUnderGroupedPageWriteLocksAsync(
                [groupId],
                async ct =>
                {
                    var entity = await repository.GroupedPageBuilderPages.FirstOrDefaultAsync(x => x.Id == groupId, ct)
                        ?? throw new KeyNotFoundException($"Page '{groupId}' not found.");
                    entity.MultilanguagePageId = multilanguagePageId;
                    await repository.UnitOfWork.CommitAsync();
                },
                cancellationToken);
        }

        GenericCachingRegion<GroupedPageBuilderPage>.ExpireTokenForKey(groupId);
        GenericSearchCachingRegion<GroupedPageBuilderPage>.ExpireRegion();
    }
}
