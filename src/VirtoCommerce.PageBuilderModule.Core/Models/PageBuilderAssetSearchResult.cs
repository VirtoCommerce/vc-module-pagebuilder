using VirtoCommerce.AssetsModule.Core.Assets;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderAssetSearchResult : BlobEntrySearchResult
{
    public int FileCount { get; set; }

    public int Skip { get; set; }
}
