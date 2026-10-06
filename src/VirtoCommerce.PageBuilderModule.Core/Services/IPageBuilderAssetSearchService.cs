using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderAssetSearchService
{
    Task<BlobEntrySearchResult> SearchAsync(PageBuilderAssetSearchCriteria criteria, CancellationToken cancellationToken = default);
}
