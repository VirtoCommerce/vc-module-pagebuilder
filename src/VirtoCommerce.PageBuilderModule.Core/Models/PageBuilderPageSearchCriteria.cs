using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// Can be used for both PageBuilderPages and GroupedPageBuilderPages
/// </summary>
public class PageBuilderPageSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }

    public string Status { get; set; }
}
