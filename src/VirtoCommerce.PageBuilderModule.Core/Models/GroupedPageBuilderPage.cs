using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class GroupedPageBuilderPage : AuditableEntity, IHasStoreId, ICloneable
{
    public string GroupId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }

    public string Status { get; set; } // Draft | Published | Archived

    public bool HasChanges
    {
        get
        {
            return Pages?.Any(p => p.Status == Draft) ?? false;
        }
    }

    public IList<PageBuilderPage> Pages { get; set; } = [];

    public string PageContent
    {
        get
        {
            if (Pages.IsNullOrEmpty())
            {
                return null;
            }

            if (Pages.Count == 1)
            {
                return Pages[0].PageContent;
            }

            var draft = Pages.FirstOrDefault(x => x.Status == Draft);
            return draft != null
                ? draft.PageContent
                : Pages.FirstOrDefault()?.PageContent;
        }
    }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
