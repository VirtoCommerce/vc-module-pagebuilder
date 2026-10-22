using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

// Mirrors how the storefront (VirtoCommerce.Pages) picks published pages for a visitor: by store, permalink, language
// (an empty one matches any), the dates the page is shown, organization (an empty one matches any) and audience.
// Anonymous visitors get public pages whatever their user groups; signed-in visitors get public and private pages
// whose user groups are empty or include one of theirs. The storefront also offers a page in the store's default
// language for other languages, but it resolves that case itself, so it is not a conflict here.
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
        if (other.Status != Published || !AreOfferedTogether(page, other))
        {
            return PageBuilderPermalinkConflict.None;
        }

        if (page.Visibility && other.Visibility)
        {
            return PageBuilderPermalinkConflict.Certain;
        }

        return UserGroupsOverlap(page, other)
            ? PageBuilderPermalinkConflict.Certain
            : PageBuilderPermalinkConflict.Possible;
    }

    private static bool AreOfferedTogether(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        return HasSamePermalink(page, other) &&
            MatchesAnyOrSame(page.CultureName, other.CultureName) &&
            MatchesAnyOrSame(page.OrganizationId, other.OrganizationId) &&
            DatesOverlap(page, other);
    }

    private static bool MatchesAnyOrSame(string value, string otherValue)
    {
        return string.IsNullOrEmpty(value) ||
            string.IsNullOrEmpty(otherValue) ||
            string.Equals(value, otherValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DatesOverlap(GroupedPageBuilderPage page, GroupedPageBuilderPage other)
    {
        return (page.StartDate ?? DateTime.MinValue) <= (other.EndDate ?? DateTime.MaxValue) &&
            (other.StartDate ?? DateTime.MinValue) <= (page.EndDate ?? DateTime.MaxValue);
    }

    // A page without user groups is offered to every signed-in visitor.
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
