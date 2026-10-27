using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

// Mirrors how the storefront (VirtoCommerce.Pages) picks a published page for a visitor. It takes the pages with the
// visitor's store and permalink that are shown now, in the visitor's language or any language, for the visitor's
// organization or any organization, and for user groups that are empty or include one of the visitor's (anonymous
// visitors get public pages only). Then the more specific page wins: the visitor's language and organization over
// any, a page for user groups over a page for everyone, a page for registered users over a public one. So a general
// page next to a specific one is intended personalization; only two equally specific pages are left to the date
// they were last changed, and that is a conflict.
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
        if (other.Status != Published ||
            !HasSamePermalink(page, other) ||
            !DatesOverlap(page, other, now) ||
            !AreEquallySpecific(page, other))
        {
            return PageBuilderPermalinkConflict.None;
        }

        return UserGroupsOverlap(page, other)
            ? PageBuilderPermalinkConflict.Certain
            : PageBuilderPermalinkConflict.Possible;
    }

    private static bool AreEquallySpecific(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        return AreSameOrBothAny(page.CultureName, other.CultureName) &&
            AreSameOrBothAny(page.OrganizationId, other.OrganizationId) &&
            page.Visibility == other.Visibility &&
            (GetUserGroups(page).Count > 0) == (GetUserGroups(other).Count > 0);
    }

    private static bool AreSameOrBothAny(string value, string otherValue)
    {
        return string.IsNullOrEmpty(value)
            ? string.IsNullOrEmpty(otherValue)
            : string.Equals(value, otherValue, StringComparison.OrdinalIgnoreCase);
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

    // Two pages for everyone share their audience; two pages for disjoint user groups meet only for a visitor in
    // both groups.
    private static bool UserGroupsOverlap(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        var userGroups = GetUserGroups(page);
        var otherUserGroups = GetUserGroups(other);
        return (userGroups.Count == 0 && otherUserGroups.Count == 0) || userGroups.Overlaps(otherUserGroups);
    }

    private static HashSet<string> GetUserGroups(GroupedPageBuilderPage page)
    {
        return (page.UserGroups ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
