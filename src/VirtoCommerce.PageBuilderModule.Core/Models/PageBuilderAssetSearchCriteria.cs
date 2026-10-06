using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderAssetSearchCriteria : SearchCriteriaBase
{
    public string FolderUrl { get; set; }

    public string[] AcceptedTypes { get; set; }

    // Upload conflict checks must find the exact file even when it is outside the current page.
    public string ExactName { get; set; }
}
