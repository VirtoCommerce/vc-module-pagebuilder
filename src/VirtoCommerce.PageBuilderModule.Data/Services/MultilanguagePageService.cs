using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
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
    IPageBuilderPageService pageService,
    IDistributedLock distributedLock,
    ILogger<MultilanguagePageService> logger)
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
    #pragma warning disable S3267 // Apply changes the group; a Where predicate should not have side effects
            foreach (var group in await GetLanguageVersionsAsync(root.Id))
            {
                if (MultilanguagePageInheritance.Apply(root, group))
                {
                    await groupedPageContentService.SaveGroupSettingsAsync(group);
                }
            }
#pragma warning restore S3267
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

    public virtual async Task<MultilanguagePage> CreateAsync(MultilanguagePage model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Id = null;
        if (!string.IsNullOrWhiteSpace(model.DefaultCultureName))
        {
            model.DefaultCultureName = await GetStoreLanguageAsync(model.StoreId, model.DefaultCultureName);
        }

        await SaveChangesAsync([model]);

        return model;
    }

    public virtual Task<MultilanguagePage> UpdateAsync(MultilanguagePage model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        return UpdateCheckedAsync(model);
    }

    private async Task<MultilanguagePage> UpdateCheckedAsync(MultilanguagePage model)
    {
        var existing = await GetRequiredAsync(model.Id);
        // A multilanguage page never moves to another store.
        model.StoreId = existing.StoreId;

        if (!string.IsNullOrWhiteSpace(model.DefaultCultureName) &&
            !string.Equals(model.DefaultCultureName, existing.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
        {
            // The source language must be served by the store and have a version, like the page a convert starts from.
            // An unchanged one is not checked: a language removed from the store must not block editing the root.
            await GetStoreLanguageAsync(existing.StoreId, model.DefaultCultureName);

            var source = (await GetLanguageVersionsAsync(existing.Id))
                .FirstOrDefault(x => string.Equals(x.CultureName, model.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Multilanguage page '{existing.Id}' has no '{model.DefaultCultureName}' version.");
            model.DefaultCultureName = source.CultureName;
        }

        await SaveChangesAsync([model]);

        return model;
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

        // Checked before the link is written: the save below would reject the page, leaving it linked.
        if (string.IsNullOrWhiteSpace(group.CultureName))
        {
            throw new InvalidOperationException($"Page '{groupId}' must have a language to become a language version.");
        }

        // A version in a language the store does not serve has no storefront address, like a version created for it.
        await GetStoreLanguageAsync(root.StoreId, group.CultureName);

        await SetMultilanguagePageIdAsync(groupId, root.Id, cancellationToken);

        try
        {
            group = await groupedPageService.GetByIdAsync(groupId);
            // The save takes the root name and marks whatever differs from the root as overridden, so the page keeps
            // serving the same permalink and settings. It also reindexes the page with its translation group.
            await groupedPageContentService.SaveGroupSettingsAsync(group, cancellationToken);
        }
        catch
        {
            // A failed or cancelled save (e.g. a concurrent version of the same language) must not leave the page linked.
            await TryRollbackAsync(() => SetMultilanguagePageIdAsync(groupId, null, CancellationToken.None), $"unlink page '{groupId}'");
            throw;
        }

        return group;
    }

    public virtual async Task<IList<string>> ResetOverridesAsync(string multilanguagePageId, IList<string> fields, CancellationToken cancellationToken = default)
    {
        var root = await GetRequiredAsync(multilanguagePageId);
        var changedCultureNames = new List<string>();

#pragma warning disable S3267 // Reset changes the group; a Where predicate should not have side effects
        foreach (var group in await GetLanguageVersionsAsync(root.Id))
        {
            // The save derives the mask again, so the reset fields become inherited.
            if (MultilanguagePageInheritance.Reset(root, group, fields ?? []))
            {
                await groupedPageContentService.SaveGroupSettingsAsync(group, cancellationToken);
                changedCultureNames.Add(group.CultureName);
            }
        }
#pragma warning restore S3267

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

        if (source?.Id == group.Id)
        {
            throw new InvalidOperationException($"Page '{groupId}' is the source language version: it is never outdated.");
        }

        // Without a source publication there is nothing to be up to date with, and the call would change nothing.
        if (source?.SourceRevisionDate == null)
        {
            throw new InvalidOperationException($"The source language '{root.DefaultCultureName}' version has not been published yet.");
        }

        group.SourceRevisionDate = source.SourceRevisionDate;
        await groupedPageService.SaveChangesAsync([group]);

        return group;
    }

    public virtual async Task<MultilanguagePage> CloneAsync(string multilanguagePageId, CancellationToken cancellationToken = default)
    {
        var source = await GetRequiredAsync(multilanguagePageId);
        var versions = await GetLanguageVersionsAsync(source.Id);

        int number;
        using (var repository = repositoryFactory())
        {
            number = await repository.FindFreeCopyNumberAsync(source.StoreId,
                [source.Permalink, .. versions.Select(x => x.Permalink)], cancellationToken);
        }

        var clone = source.CloneTyped();
        clone.Id = null;
        clone.Name = PageBuilderPageCopy.GetName(source.Name, number);
        clone.Permalink = PageBuilderPageCopy.GetPermalink(source.Permalink, number);
        await SaveChangesAsync([clone]);

        foreach (var version in versions)
        {
            var content = (await groupedPageContentService.LoadGroupContentAsync(version, draft: true, cancellationToken)).Content;

            var group = AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance();
            group.StoreId = clone.StoreId;
            group.CultureName = version.CultureName;
            group.MultilanguagePageId = clone.Id;
            group.Permalink = PageBuilderPageCopy.GetPermalink(version.Permalink, number);
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
            await TryRollbackAsync(() => base.DeleteAsync([root.Id]), $"delete multilanguage page '{root.Id}'");
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

        return await distributedLock.ExecuteAsync(GetBatchLockResource(root.Id), _ => PublishAllUnderLockAsync(root),
            cancellationToken: cancellationToken);
    }

    private async Task<MultilanguagePageBatchResult> PublishAllUnderLockAsync(MultilanguagePage root)
    {
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

            var published = false;
            try
            {
                await groupedPageService.SaveChangesAsync([group]);
                published = true;
                await pageService.DeleteAsync(obsoletePageIds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failing language does not stop the others; the summary reports it.
                logger.LogError(ex, "Publishing the {CultureName} version '{GroupId}' of multilanguage page '{MultilanguagePageId}' failed.",
                    group.CultureName, group.Id, root.Id);
                // The publish changed a shared cached copy of the version, so the cache must not serve it.
                GenericCachingRegion<GroupedPageBuilderPage>.ExpireTokenForKey(group.Id);
                GenericSearchCachingRegion<GroupedPageBuilderPage>.ExpireRegion();
                if (!published)
                {
                    result.Skipped.Add(new MultilanguagePageBatchSkip { CultureName = group.CultureName, Reason = MultilanguagePageBatchResult.FailedReason });
                    continue;
                }
            }

            // A version whose obsolete pages were not removed is still published; they are cleaned up on its next publish.
            result.ProcessedCultureNames.Add(group.CultureName);
        }

        return result;
    }

    public virtual async Task<MultilanguagePageBatchResult> ArchiveAllAsync(string multilanguagePageId, CancellationToken cancellationToken = default)
    {
        var root = await GetRequiredAsync(multilanguagePageId);

        return await distributedLock.ExecuteAsync(GetBatchLockResource(root.Id), _ => ArchiveAllUnderLockAsync(root),
            cancellationToken: cancellationToken);
    }

    private async Task<MultilanguagePageBatchResult> ArchiveAllUnderLockAsync(MultilanguagePage root)
    {
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

    // Batches of one page run one at a time and read the versions under the lock: two parallel publish-all calls would
    // otherwise both publish the same drafts, and the source revision of the second save would not mark translations outdated.
    private static string GetBatchLockResource(string multilanguagePageId)
    {
        return $"pagebuilder:multilanguage-page:batch:{multilanguagePageId}";
    }

    // A failing rollback is logged, so the caller still gets the error that caused it.
    private async Task TryRollbackAsync(Func<Task> rollback, string description)
    {
        try
        {
            await rollback();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: could not {Rollback}.", description);
        }
    }

    // The store spelling of the language ("en-US" for "EN-us"): versions, the index and the storefront compare it as stored.
    private async Task<string> GetStoreLanguageAsync(string storeId, string cultureName)
    {
        var store = await storeService.GetNoCloneAsync(storeId);

        return store?.Languages?.FirstOrDefault(x => string.Equals(x, cultureName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"'{cultureName}' is not a language of store '{storeId}'.");
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
