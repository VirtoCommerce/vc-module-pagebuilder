using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// Status transitions of a grouped page shared by single-page and batch (multilanguage) actions.
/// </summary>
public static class GroupedPagePublishing
{
    /// <summary>
    /// Publishes the current draft. The other pages of the group become obsolete and must be deleted by the caller after saving the group.
    /// </summary>
    /// <returns><c>false</c> when the group has no draft; the group is left unchanged.</returns>
    public static bool TryPublishDraft(GroupedPageBuilderPage group, out IList<string> obsoletePageIds)
    {
        var draft = PageBuilderPageSelection.Order(group.Pages).FirstOrDefault(x => x.Status == Draft);
        if (draft == null)
        {
            obsoletePageIds = [];
            return false;
        }

        draft.Status = Published;
        obsoletePageIds = group.Pages.Where(x => x.Id != draft.Id).Select(x => x.Id).ToList();
        return true;
    }

    /// <returns><c>false</c> when every page is already archived.</returns>
    public static bool Archive(GroupedPageBuilderPage group)
    {
        var changed = false;

        foreach (var page in group.Pages.Where(x => x.Status != Archived))
        {
            page.Status = Archived;
            changed = true;
        }

        return changed;
    }
}
