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

            if (string.IsNullOrWhiteSpace(model.DefaultCultureName) && !string.IsNullOrEmpty(model.Id))
            {
                // A client that does not send the source language keeps it.
                model.DefaultCultureName = (await this.GetNoCloneAsync(model.Id))?.DefaultCultureName;
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

    public virtual async Task<IList<string>> ResetOverridesAsync(string multilanguagePageId, IList<string> fields, CancellationToken cancellationToken = default)
    {
        var root = await GetRequiredAsync(multilanguagePageId);
        var changedCultureNames = new List<string>();

        foreach (var group in await GetLanguageVersionsAsync(root.Id))
        {
            // The save derives the mask again, so the reset fields become inherited.
            if (MultilanguagePageInheritance.Reset(root, group, fields ?? []))
            {
                await groupedPageContentService.SaveGroupSettingsAsync(group, cancellationToken);
                changedCultureNames.Add(group.CultureName);
            }
        }

        return changedCultureNames;
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

    public virtual async Task<MultilanguagePage> CloneAsync(string multilanguagePageId, CancellationToken cancellationToken = default)
    {
        var source = await GetRequiredAsync(multilanguagePageId);

        var clone = (MultilanguagePage)source.Clone();
        clone.Id = null;
        clone.Name = $"{source.Name} (copy)";
        clone.Permalink = WithCopySuffix(source.Permalink);
        await SaveChangesAsync([clone]);

        foreach (var version in await GetLanguageVersionsAsync(source.Id))
        {
            var content = (await groupedPageContentService.LoadGroupContentAsync(version, draft: true, cancellationToken)).Content;

            var group = AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance();
            group.StoreId = clone.StoreId;
            group.CultureName = version.CultureName;
            group.MultilanguagePageId = clone.Id;
            group.Permalink = WithCopySuffix(version.Permalink);
            group.Visibility = version.Visibility;
            group.UserGroups = version.UserGroups;
            group.OrganizationId = version.OrganizationId;
            group.StartDate = version.StartDate;
            group.EndDate = version.EndDate;
            group.SourceRevisionDate = version.SourceRevisionDate;
            // Overridden fields keep the version's values, the others follow the copy's root.
            group.OverriddenFields = version.OverriddenFields;
            MultilanguagePageInheritance.Apply(clone, group);

            var draft = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
            draft.Status = ModuleConstants.PageStatuses.Draft;
            draft.StoreId = clone.StoreId;
            draft.Content = PageBuilderContentSettings.Synchronize(content, group);
            group.Pages.Add(draft);

            await groupedPageService.SaveChangesAsync([group]);
        }

        return clone;
    }

    private static string WithCopySuffix(string permalink)
    {
        return string.IsNullOrEmpty(permalink) ? permalink : $"{permalink.TrimEnd('/')}-copy";
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

        // The page becomes the source language, so its language must be one the store serves.
        var store = await storeService.GetByIdAsync(group.StoreId);
        if (string.IsNullOrWhiteSpace(group.CultureName) ||
            store?.Languages?.Contains(group.CultureName, StringComparer.OrdinalIgnoreCase) != true)
        {
            throw new InvalidOperationException($"Page '{groupId}' must have a language of store '{group.StoreId}'.");
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

        try
        {
            await LinkAsync(root.Id, groupId, cancellationToken);
        }
        catch
        {
            // The root exists only for this page; without it the root would stay as an empty Multilanguage Page.
            await base.DeleteAsync([root.Id]);
            throw;
        }

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

            try
            {
                await groupedPageService.SaveChangesAsync([group]);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failing language does not stop the others; the summary reports it.
                // The publish changed a shared cached copy of the version, so the cache must not serve it.
                GenericCachingRegion<GroupedPageBuilderPage>.ExpireTokenForKey(group.Id);
                GenericSearchCachingRegion<GroupedPageBuilderPage>.ExpireRegion();
                result.Skipped.Add(new MultilanguagePageBatchSkip { CultureName = group.CultureName, Reason = MultilanguagePageBatchResult.FailedReason });
                continue;
            }

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
                    // The revision a translation was based on belongs to the previous root, if any.
                    entity.SourceRevisionDate = null;
                    await repository.UnitOfWork.CommitAsync();
                },
                cancellationToken);
        }

        GenericCachingRegion<GroupedPageBuilderPage>.ExpireTokenForKey(groupId);
        GenericSearchCachingRegion<GroupedPageBuilderPage>.ExpireRegion();
    }
}
