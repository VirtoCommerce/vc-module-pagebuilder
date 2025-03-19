using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class GroupedPageBuilderPage : AuditableEntity, ICloneable
{
    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }

    public bool HasChanges { get; set; }

    public string Status { get; set; } // Draft | Published | Archived

    public IList<string> PageIds { get; set; } = [];

    public IList<PageBuilderPage> Pages { get; set; } = [];

    public string PageContent
    {
        get
        {
            if (Pages.IsNullOrEmpty())
            {
                return null;
            }

            var draft = Pages.FirstOrDefault(x => x.Status == Draft);
            if (draft != null)
            {
                return draft.PageContent;
            }
            else
            {
                return Pages.FirstOrDefault()?.PageContent;
            }
        }
    }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
