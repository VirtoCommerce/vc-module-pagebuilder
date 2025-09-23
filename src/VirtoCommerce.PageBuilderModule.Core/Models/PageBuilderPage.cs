using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderPage : AuditableEntity, IHasStoreId, ICloneable
{
    public string GroupId { get; set; }

    public string StoreId { get; set; }
    public string Status { get; set; } // Draft | Published | Archived

    public object Clone()
    {
        return MemberwiseClone();
    }
}
