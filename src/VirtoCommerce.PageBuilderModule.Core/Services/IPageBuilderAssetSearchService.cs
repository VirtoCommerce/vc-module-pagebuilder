using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderAssetSearchService
{
    Task<PageBuilderAssetSearchResult> SearchAsync(PageBuilderAssetSearchCriteria criteria, CancellationToken cancellationToken = default);
}
