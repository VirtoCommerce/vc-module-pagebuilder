using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class MultilanguagePageSearchCriteria : SearchCriteriaBase, IHasStoreId
{
    public string StoreId { get; set; }
}
