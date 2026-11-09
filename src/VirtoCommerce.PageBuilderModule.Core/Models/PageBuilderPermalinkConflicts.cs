using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

// Mirrors how the storefront (VirtoCommerce.Pages) picks a published page for a visitor. It takes the pages with the
// visitor's store and permalink that are shown now, in the visitor's language, the store's default language or any
// language, for the visitor's organization or any organization, and for user groups that are empty or include one
// of the visitor's (anonymous visitors get public pages for every user group only). Then the more specific page
// wins: the visitor's language over the store's default language over any, the visitor's organization over any,
// a page for user groups over a page for everyone, a page for registered users over a public one; between equally
// specific pages, the most recently changed one.
// So a general page next to a specific one is intended personalization. Two equally specific pages compete, and the
// one published now wins where they meet: a certain conflict when it covers all the visitors and dates of the other
// page, which then is never shown; a possible conflict when the other page still has visitors or dates of its own.
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
        return GetConflict(page, other, now, storeDefaultLanguage: null);
    }

    /// <summary>
    /// The conflict as of <paramref name="now"/> in a store whose default language is
    /// <paramref name="storeDefaultLanguage"/>: the storefront prefers a page in that language to a page for any
    /// language for every visitor, so the two are equally specific. Without it, any language is only like any language.
    /// </summary>
    public static PageBuilderPermalinkConflict GetConflict(GroupedPageBuilderPage page, GroupedPageBuilderPage other,
        DateTime now, string storeDefaultLanguage)
    {
        if (other.Status != Published ||
            !HasSamePermalink(page, other) ||
            !DatesOverlap(page, other, now) ||
            !AreEquallySpecific(page, other, storeDefaultLanguage))
        {
            return PageBuilderPermalinkConflict.None;
        }

        return CoversUserGroups(page, other) && CoversDates(page, other, now)
            ? PageBuilderPermalinkConflict.Certain
            : PageBuilderPermalinkConflict.Possible;
    }

    private static bool AreEquallySpecific(GroupedPageBuilderPage page, GroupedPageBuilderPage other, string storeDefaultLanguage)
    {
        return AreSameOrBothAny(GetLanguage(page, storeDefaultLanguage), GetLanguage(other, storeDefaultLanguage)) &&
            AreSameOrBothAny(page.OrganizationId, other.OrganizationId) &&
            HaveSameKindOfAudience(page, other);
    }

    private static string GetLanguage(GroupedPageBuilderPage page, string storeDefaultLanguage)
    {
        return string.IsNullOrEmpty(page.CultureName) ? storeDefaultLanguage : page.CultureName;
    }

    private static bool AreSameOrBothAny(string value, string otherValue)
    {
        return string.IsNullOrEmpty(value)
            ? string.IsNullOrEmpty(otherValue)
            : string.Equals(value, otherValue, StringComparison.OrdinalIgnoreCase);
    }

    // Pages for user groups are offered to signed-in members only, whatever their visibility; pages for everyone
    // differ by visibility: public pages also go to anonymous visitors.
    private static bool HaveSameKindOfAudience(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        var hasUserGroups = GetUserGroups(page).Count > 0;
        if (hasUserGroups != (GetUserGroups(other).Count > 0))
        {
            return false;
        }

        return hasUserGroups || page.Visibility == other.Visibility;
    }

    // Both pages are shown at some moment from now on.
    private static bool DatesOverlap(GroupedPageBuilderPage page, GroupedPageBuilderPage other, DateTime now)
    {
        var from = Max(Max(page.StartDate ?? DateTime.MinValue, other.StartDate ?? DateTime.MinValue), now);
        var to = Min(page.EndDate ?? DateTime.MaxValue, other.EndDate ?? DateTime.MaxValue);
        return from <= to;
    }

    // The page is shown for the whole rest of the other page's period.
    private static bool CoversDates(GroupedPageBuilderPage page, GroupedPageBuilderPage other, DateTime now)
    {
        var otherFrom = Max(other.StartDate ?? DateTime.MinValue, now);
        var otherTo = other.EndDate ?? DateTime.MaxValue;
        return (page.StartDate ?? DateTime.MinValue) <= otherFrom &&
            (page.EndDate ?? DateTime.MaxValue) >= otherTo;
    }

    // Every member the other page is for also gets the page: both are for everyone (no user groups), or the other
    // page's user groups are among the page's.
    private static bool CoversUserGroups(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        return GetUserGroups(other).IsSubsetOf(GetUserGroups(page));
    }

    private static DateTime Max(DateTime value, DateTime other) => value > other ? value : other;

    private static DateTime Min(DateTime value, DateTime other) => value < other ? value : other;

    private static HashSet<string> GetUserGroups(GroupedPageBuilderPage page)
    {
        return (page.UserGroups ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
