using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderPageSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }

    public string Status { get; set; }
}
