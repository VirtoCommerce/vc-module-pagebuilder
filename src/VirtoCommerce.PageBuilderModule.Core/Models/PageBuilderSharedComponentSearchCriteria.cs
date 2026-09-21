using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderSharedComponentSearchCriteria : SearchCriteriaBase, IHasStoreId
{
    public string StoreId { get; set; }
}
