namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderAssetReference
{
    public string AssetUrl { get; set; }

    public string NormalizedAssetUrl { get; set; }

    public int ReferencesCount { get; set; }

    public int PageReferencesCount { get; set; }

    public int SharedComponentReferencesCount { get; set; }

    public IList<PageBuilderAssetReferencePage> Pages { get; set; } = [];

    public IList<PageBuilderAssetReferenceSharedComponent> SharedComponents { get; set; } = [];
}
