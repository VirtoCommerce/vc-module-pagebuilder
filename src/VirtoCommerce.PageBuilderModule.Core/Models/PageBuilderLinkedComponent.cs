using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderLinkedComponent : AuditableEntity, IHasStoreId, ICloneable
{
    public string StoreId { get; set; }

    public string Name { get; set; }

    public int UsageCount { get; set; }

    public IList<PageBuilderLinkedComponentUsagePage> UsagePages { get; set; } = [];

    public object Clone()
    {
        var clone = (PageBuilderLinkedComponent)MemberwiseClone();
        clone.UsagePages = UsagePages
            .Select(x => new PageBuilderLinkedComponentUsagePage
            {
                Id = x.Id,
                Name = x.Name,
                Permalink = x.Permalink,
                CultureName = x.CultureName,
                Status = x.Status,
            })
            .ToList();

        return clone;
    }
}
