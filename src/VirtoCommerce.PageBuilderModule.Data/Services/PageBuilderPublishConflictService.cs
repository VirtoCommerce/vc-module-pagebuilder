using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderPublishConflictService(
    IGroupedPageService groupedPageService,
    IGroupedPageSearchService groupedPageSearchService,
    IStoreService storeService,
    IDistributedLock distributedLock)
    : IPageBuilderPublishConflictService
{
    public virtual async Task<IList<PageBuilderPermalinkUsage>> FindPermalinkUsagesAsync(GroupedPageBuilderPage page)
    {
        return await groupedPageSearchService.FindPermalinkUsagesAsync(page, await GetStoreDefaultLanguageAsync(page.StoreId));
    }

    // The search results can be cached, so each group is checked again as reloaded: one that changed its permalink,
    // dates or audience meanwhile, or was unpublished or deleted, is left alone, and one that got a draft cannot be
    // unpublished.
    public virtual async Task<(IList<PageBuilderPermalinkUsage> Conflicts, IList<GroupedPageBuilderPage> Groups)> FindBlockingConflictsAsync(
        GroupedPageBuilderPage page)
    {
        var storeDefaultLanguage = await GetStoreDefaultLanguageAsync(page.StoreId);
        var candidates = (await groupedPageSearchService.FindPermalinkUsagesAsync(page, storeDefaultLanguage))
            .Where(x => x.Conflict == PageBuilderPermalinkConflict.Certain)
            .ToList();
        if (candidates.Count == 0)
        {
            return ([], []);
        }

        var groups = (await groupedPageService.GetAsync(candidates.Select(x => x.GroupId).ToList()))
            .Where(x => PageBuilderPermalinkConflicts.GetConflict(page, x, DateTime.UtcNow, storeDefaultLanguage) == PageBuilderPermalinkConflict.Certain)
            .ToList();
        var conflicts = candidates
            .Where(usage => groups.Any(group => group.Id == usage.GroupId))
            .ToList();
        foreach (var usage in conflicts)
        {
            usage.CanUnpublish = !groups.First(group => group.Id == usage.GroupId).HasChanges;
        }

        return (conflicts, groups);
    }

    // Two pages that conflict only with each other must not both pass the check before either is saved. A conflict
    // needs the same store and permalink, so the lock is on them.
    public virtual Task<T> ExecuteExclusiveAsync<T>(GroupedPageBuilderPage page, Func<Task<T>> publish, CancellationToken cancellationToken = default)
    {
        if (distributedLock == null)
        {
            return publish();
        }

        var resource = $"pagebuilder:publish:{page.StoreId?.ToLowerInvariant()}:{PageBuilderPermalinkConflicts.Normalize(page.Permalink)}";

        return distributedLock.ExecuteAsync(resource, _ => publish(), cancellationToken: cancellationToken);
    }

    // A page for any language competes with a page in the store's default language. Unknown without a store service.
    protected virtual async Task<string> GetStoreDefaultLanguageAsync(string storeId)
    {
        if (storeService == null || string.IsNullOrEmpty(storeId))
        {
            return null;
        }

        var store = await storeService.GetNoCloneAsync(storeId);
        return store?.DefaultLanguage;
    }
}
