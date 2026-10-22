using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// Can be used for both PageBuilderPages and GroupedPageBuilderPages
/// </summary>
public class PageBuilderPageSearchCriteria : SearchCriteriaBase, IHasStoreId
{
    public string StoreId { get; set; }

    public string Statuses { get; set; }

    /// <summary>
    /// Matches grouped pages with this permalink, ignoring case and the leading slash.
    /// </summary>
    public string Permalink { get; set; }

    public string Lifecycle { get; set; }

    public DateTime? ActiveOn { get; set; }

    public DateTime? ModifiedSince { get; set; }
    public DateTime? ModifiedBefore { get; set; }
}
