using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public static class GroupedPageSearchServiceExtensions
{
    /// <summary>
    /// Other pages of the same store that use the permalink of <paramref name="page"/>, except archived ones,
    /// with the conflict that publishing <paramref name="page"/> creates with each of them.
    /// </summary>
    public static async Task<IList<PageBuilderPermalinkUsage>> FindPermalinkUsagesAsync(
        this IGroupedPageSearchService searchService,
        GroupedPageBuilderPage page)
    {
        if (PageBuilderPermalinkConflicts.Normalize(page.Permalink) == null || string.IsNullOrEmpty(page.StoreId))
        {
            return [];
        }

        var criteria = AbstractTypeFactory<PageBuilderPageSearchCriteria>.TryCreateInstance();
        criteria.StoreId = page.StoreId;
        criteria.Permalink = page.Permalink;
        var groups = await searchService.SearchAllNoCloneAsync(criteria);

        return groups
            .Where(x => !string.Equals(x.Id, page.Id, StringComparison.OrdinalIgnoreCase) &&
                x.Status != Archived &&
                PageBuilderPermalinkConflicts.HasSamePermalink(page, x))
            .Select(x => ToPermalinkUsage(page, x))
            .ToList();
    }

    private static PageBuilderPermalinkUsage ToPermalinkUsage(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        var usage = AbstractTypeFactory<PageBuilderPermalinkUsage>.TryCreateInstance();
        usage.GroupId = other.Id;
        usage.Name = other.Name;
        usage.Permalink = other.Permalink;
        usage.CultureName = other.CultureName;
        usage.Status = other.Status;
        usage.Conflict = PageBuilderPermalinkConflicts.GetConflict(page, other);
        // Unpublishing is refused while a draft exists alongside the published page.
        usage.CanUnpublish = !other.HasChanges;
        return usage;
    }
}
