using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderAssetReferencesSearchCriteria : SearchCriteriaBase, IHasStoreId
{
    public string StoreId { get; set; }

    public string Statuses { get; set; }

    public bool IncludePages { get; set; }

    public IList<string> AssetUrls { get; set; } = [];
}

public class PageBuilderAssetReferencesSearchResult : GenericSearchResult<PageBuilderAssetReference>
{
}

public class PageBuilderAssetReference
{
    public string AssetUrl { get; set; }

    public string NormalizedAssetUrl { get; set; }

    public int ReferencesCount { get; set; }

    public IList<PageBuilderAssetReferencePage> Pages { get; set; } = [];
}

public class PageBuilderAssetReferencePage
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }

    public string CultureName { get; set; }

    public string Status { get; set; }
}
