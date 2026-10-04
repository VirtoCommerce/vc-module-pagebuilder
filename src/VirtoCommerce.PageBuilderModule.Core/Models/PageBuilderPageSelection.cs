using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderPageSelection
{
    // Use the same authority and tie-break order for reading, saving, publishing and copying.
    public static IOrderedEnumerable<PageBuilderPage> Order(IEnumerable<PageBuilderPage> pages, bool draft = true)
    {
        return pages.Where(x => (draft && x.Status == Draft) || x.Status is Published or Archived)
            .OrderBy(x => x.Status switch { Draft => 0, Published => 1, _ => 2 })
            .ThenByDescending(x => x.ModifiedDate).ThenBy(x => x.Id, StringComparer.Ordinal);
    }
}
