using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

// Mirrors how the storefront (VirtoCommerce.Pages) picks published pages for a visitor: by store, permalink, language
// (an empty one matches any), the dates the page is shown from now on, organization (an empty one matches any) and
// user groups. Anonymous visitors get public pages for every user group; signed-in visitors get public and private
// pages whose user groups are empty or include one of theirs. So visibility alone never separates two pages: only
// disjoint user groups do. The storefront also offers a page in the store's default language for other languages,
// but it resolves that case itself, so it is not a conflict here.
public static class PageBuilderPermalinkConflicts
{
    public static string Normalize(string permalink)
    {
        return string.IsNullOrWhiteSpace(permalink)
            ? null
            : "/" + permalink.Trim().TrimStart('/').ToLowerInvariant();
    }

    public static bool HasSamePermalink(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        var permalink = Normalize(page.Permalink);
        return permalink != null &&
            permalink == Normalize(other.Permalink) &&
            string.Equals(page.StoreId, other.StoreId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The conflict that publishing <paramref name="page"/> creates with <paramref name="other"/>.
    /// Only a published <paramref name="other"/> can conflict: drafts never reach the storefront.
    /// </summary>
    public static PageBuilderPermalinkConflict GetConflict(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        return GetConflict(page, other, DateTime.UtcNow);
    }

    /// <summary>
    /// The conflict as of <paramref name="now"/>: a page whose dates are over is never shown again, so it does not
    /// conflict.
    /// </summary>
    public static PageBuilderPermalinkConflict GetConflict(GroupedPageBuilderPage page, GroupedPageBuilderPage other, DateTime now)
    {
        if (other.Status != Published || !AreOfferedTogether(page, other, now))
        {
            return PageBuilderPermalinkConflict.None;
        }

        return UserGroupsOverlap(page, other)
            ? PageBuilderPermalinkConflict.Certain
            : PageBuilderPermalinkConflict.Possible;
    }

    private static bool AreOfferedTogether(GroupedPageBuilderPage page, GroupedPageBuilderPage other, DateTime now)
    {
        return HasSamePermalink(page, other) &&
            MatchesAnyOrSame(page.CultureName, other.CultureName) &&
            MatchesAnyOrSame(page.OrganizationId, other.OrganizationId) &&
            DatesOverlap(page, other, now);
    }

    private static bool MatchesAnyOrSame(string value, string otherValue)
    {
        return string.IsNullOrEmpty(value) ||
            string.IsNullOrEmpty(otherValue) ||
            string.Equals(value, otherValue, StringComparison.OrdinalIgnoreCase);
    }

    // Both pages are shown at some moment from now on.
    private static bool DatesOverlap(GroupedPageBuilderPage page, GroupedPageBuilderPage other, DateTime now)
    {
        var from = Max(Max(page.StartDate ?? DateTime.MinValue, other.StartDate ?? DateTime.MinValue), now);
        var to = Min(page.EndDate ?? DateTime.MaxValue, other.EndDate ?? DateTime.MaxValue);
        return from <= to;
    }

    private static DateTime Max(DateTime value, DateTime other) => value > other ? value : other;

    private static DateTime Min(DateTime value, DateTime other) => value < other ? value : other;

    // A page without user groups is offered to every visitor its visibility allows.
    private static bool UserGroupsOverlap(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        var userGroups = GetUserGroups(page);
        var otherUserGroups = GetUserGroups(other);
        return userGroups.Count == 0 || otherUserGroups.Count == 0 || userGroups.Overlaps(otherUserGroups);
    }

    private static HashSet<string> GetUserGroups(GroupedPageBuilderPage page)
    {
        return (page.UserGroups ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
