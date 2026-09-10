using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderSharedComponent : AuditableEntity, IHasStoreId, ICloneable
{
    public string StoreId { get; set; }

    public string Name { get; set; }

    public int UsageCount { get; set; }

    public IList<PageBuilderSharedComponentUsagePage> UsagePages { get; set; } = [];

    public object Clone()
    {
        var clone = (PageBuilderSharedComponent)MemberwiseClone();
        clone.UsagePages = UsagePages
            .Select(x => new PageBuilderSharedComponentUsagePage
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
