using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class GroupedPageBuilderPage : AuditableEntity, IHasStoreId, ICloneable
{
    public string StoreId { get; set; }

    public IList<PageBuilderPage> Pages { get; set; } = [];

    public string GroupStatus
    {
        get
        {
            return Pages?.All(x => x.Status == Archived) ?? false
                ? Archived
                : Pages?.Any(x => x.Status == Published) ?? false
                    ? Published
                    : Draft;
        }
    }

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
