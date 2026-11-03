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

    public string Lifecycle { get; set; }

    public DateTime? ActiveOn { get; set; }

    public DateTime? ModifiedSince { get; set; }
    public DateTime? ModifiedBefore { get; set; }

    /// <summary>
    /// Grouped pages only: language versions of this <see cref="MultilanguagePage"/>.
    /// </summary>
    public string MultilanguagePageId { get; set; }

    /// <summary>
    /// Pages list only: <see cref="PageTypes.Landing"/> or <see cref="PageTypes.Multilanguage"/>; empty lists both.
    /// </summary>
    public string PageType { get; set; }

    /// <summary>
    /// Pages list only: <see cref="TranslationStatuses.Missing"/> or <see cref="TranslationStatuses.Outdated"/>, in
    /// <see cref="SearchCriteriaBase.LanguageCode"/> when set, otherwise in any store language. Lists Multilanguage Pages only.
    /// </summary>
    public string TranslationStatus { get; set; }
}
