using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderPage : AuditableEntity, ICloneable
{
    public string GroupId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }

    public string Status { get; set; } // Draft | Published | Archived

    public string PageContent { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
