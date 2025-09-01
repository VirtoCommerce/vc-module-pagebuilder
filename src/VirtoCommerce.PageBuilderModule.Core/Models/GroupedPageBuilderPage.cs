using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class GroupedPageBuilderPage : AuditableEntity, IHasStoreId, ICloneable
{
    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }

    public string Status { get; set; } // Draft | Published | Archived

    public bool Visibility { get; set; }
    public string UserGroups { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public IList<PageBuilderPage> Pages { get; set; } = [];

    public bool HasChanges
    {
        get
        {
            return Pages?.Any(p => p.Status == Draft) ?? false;
        }
    }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
