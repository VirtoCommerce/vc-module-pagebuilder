using Microsoft.EntityFrameworkCore;
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
    IStoreService storeService,
    IPageBuilderPageService pageService)
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
            foreach (var group in await GetLanguageVersionsAsync(root.Id))
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
            foreach (var group in await GetLanguageVersionsAsync(id))
            {
                await UnlinkAsync(group.Id);
            }
        }

        await base.DeleteAsync(ids, softDelete);
    }

    public virtual async Task<GroupedPageBuilderPage> LinkAsync(string multilanguagePageId, string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        var root = await GetRequiredAsync(multilanguagePageId);
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

        var languages = await GetLanguageVersionsAsync(root.Id);
        if (languages.Any(x => string.Equals(x.CultureName, group.CultureName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Multilanguage page '{root.Id}' already has a '{group.CultureName}' version.");
        }

        await SetMultilanguagePageIdAsync(groupId, root.Id, cancellationToken);

        group = await groupedPageService.GetByIdAsync(groupId);
        // The save takes the root name and marks whatever differs from the root as overridden, so the page keeps
        // serving the same permalink and settings. It also reindexes the page with its translation group.
        await groupedPageContentService.SaveGroupSettingsAsync(group, cancellationToken);

        return group;
    }

    public virtual async Task<GroupedPageBuilderPage> MarkUpToDateAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        var group = await groupedPageService.GetByIdAsync(groupId)
            ?? throw new KeyNotFoundException($"Page '{groupId}' not found.");
        var root = await GetRequiredAsync(group.MultilanguagePageId);

        var source = (await GetLanguageVersionsAsync(root.Id))
            .FirstOrDefault(x => string.Equals(x.CultureName, root.DefaultCultureName, StringComparison.OrdinalIgnoreCase));

        if (source?.SourceRevisionDate != null && source.Id != group.Id)
        {
            group.SourceRevisionDate = source.SourceRevisionDate;
            await groupedPageService.SaveChangesAsync([group]);
        }

        return group;
    }

    public virtual async Task<MultilanguagePage> ConvertAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        var group = await groupedPageService.GetByIdAsync(groupId)
            ?? throw new KeyNotFoundException($"Page '{groupId}' not found.");

        if (!string.IsNullOrEmpty(group.MultilanguagePageId))
        {
            throw new InvalidOperationException($"Page '{groupId}' is already a language version of '{group.MultilanguagePageId}'.");
        }

        var root = AbstractTypeFactory<MultilanguagePage>.TryCreateInstance();
        root.StoreId = group.StoreId;
        root.Name = group.Name;
        root.Permalink = group.Permalink;
        root.DefaultCultureName = group.CultureName;
        root.OrganizationId = group.OrganizationId;
        root.Visibility = group.Visibility;
        root.UserGroups = group.UserGroups;
        root.StartDate = group.StartDate;
        root.EndDate = group.EndDate;

        await SaveChangesAsync([root]);
        await LinkAsync(root.Id, groupId, cancellationToken);

        return root;
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

    public virtual async Task<IList<MultilanguagePageLanguage>> GetLanguagesAsync(string multilanguagePageId, CancellationToken cancellationToken = default)
    {
        var root = await GetRequiredAsync(multilanguagePageId);
        var store = await storeService.GetByIdAsync(root.StoreId);

        return MultilanguagePageLanguages.Build(root, store?.Languages, await GetLanguageVersionsAsync(root.Id));
    }

    public virtual async Task<GroupedPageBuilderPage> CreateLanguageAsync(string multilanguagePageId, string cultureName, bool copyContent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        var root = await GetRequiredAsync(multilanguagePageId);
        var language = (await GetLanguagesAsync(root.Id, cancellationToken))
            .FirstOrDefault(x => string.Equals(x.CultureName, cultureName, StringComparison.OrdinalIgnoreCase));

        if (language is not { IsStoreLanguage: true })
        {
            throw new InvalidOperationException($"'{cultureName}' is not a language of store '{root.StoreId}'.");
        }

        if (language.Page != null)
        {
            throw new InvalidOperationException($"Multilanguage page '{root.Id}' already has a '{language.CultureName}' version.");
        }

        var group = AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance();
        group.StoreId = root.StoreId;
        group.CultureName = language.CultureName;
        group.MultilanguagePageId = root.Id;
        MultilanguagePageInheritance.Apply(root, group);

        string sourceContent = null;
        if (copyContent)
        {
            var source = (await GetLanguageVersionsAsync(root.Id))
                .FirstOrDefault(x => string.Equals(x.CultureName, root.DefaultCultureName, StringComparison.OrdinalIgnoreCase));
            if (source != null)
            {
                sourceContent = (await groupedPageContentService.LoadGroupContentAsync(source, draft: true, cancellationToken)).Content;
                // A copy of the source is up to date with it.
                group.SourceRevisionDate = source.SourceRevisionDate;
            }
        }

        var draft = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
        draft.Status = ModuleConstants.PageStatuses.Draft;
        draft.StoreId = root.StoreId;
        // The copied document still carries the source language settings (permalink, culture).
        draft.Content = PageBuilderContentSettings.Synchronize(sourceContent, group);
        group.Pages.Add(draft);

        await groupedPageService.SaveChangesAsync([group]);

        return group;
    }

    public virtual async Task<MultilanguagePageBatchResult> PublishAllAsync(string multilanguagePageId, CancellationToken cancellationToken = default)
    {
        var root = await GetRequiredAsync(multilanguagePageId);
        var result = new MultilanguagePageBatchResult();

        // The source language goes first: translations published in the same batch are based on its new revision.
        var groups = (await GetLanguageVersionsAsync(root.Id))
            .OrderByDescending(x => string.Equals(x.CultureName, root.DefaultCultureName, StringComparison.OrdinalIgnoreCase));

        foreach (var group in groups)
        {
            if (!GroupedPagePublishing.TryPublishDraft(group, out var obsoletePageIds))
            {
                result.Skipped.Add(new MultilanguagePageBatchSkip { CultureName = group.CultureName, Reason = MultilanguagePageBatchResult.NoDraftReason });
                continue;
            }

            await groupedPageService.SaveChangesAsync([group]);
            await pageService.DeleteAsync(obsoletePageIds);
            result.ProcessedCultureNames.Add(group.CultureName);
        }

        return result;
    }

    public virtual async Task<MultilanguagePageBatchResult> ArchiveAllAsync(string multilanguagePageId, CancellationToken cancellationToken = default)
    {
        var root = await GetRequiredAsync(multilanguagePageId);
        var result = new MultilanguagePageBatchResult();
        var changed = new List<GroupedPageBuilderPage>();

        foreach (var group in await GetLanguageVersionsAsync(root.Id))
        {
            if (GroupedPagePublishing.Archive(group))
            {
                changed.Add(group);
                result.ProcessedCultureNames.Add(group.CultureName);
            }
            else
            {
                result.Skipped.Add(new MultilanguagePageBatchSkip { CultureName = group.CultureName, Reason = MultilanguagePageBatchResult.AlreadyArchivedReason });
            }
        }

        if (changed.Count > 0)
        {
            await groupedPageService.SaveChangesAsync(changed);
        }

        return result;
    }

    private async Task<MultilanguagePage> GetRequiredAsync(string multilanguagePageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(multilanguagePageId);

        return await this.GetByIdAsync(multilanguagePageId)
            ?? throw new KeyNotFoundException($"Multilanguage page '{multilanguagePageId}' not found.");
    }

    protected virtual async Task<IList<GroupedPageBuilderPage>> GetLanguageVersionsAsync(string multilanguagePageId)
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
