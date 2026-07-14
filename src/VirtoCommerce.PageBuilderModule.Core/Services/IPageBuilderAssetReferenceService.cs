using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderAssetReferenceService
{
    Task<PageBuilderAssetReferencesSearchResult> SearchReferencesAsync(PageBuilderAssetReferencesSearchCriteria criteria, CancellationToken cancellationToken = default);
}
