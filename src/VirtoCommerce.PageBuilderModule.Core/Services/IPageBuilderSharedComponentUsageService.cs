using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentUsageService
{
    Task<IList<PageBuilderSharedComponentUsage>> GetUsageAsync(
        IEnumerable<string> sharedComponentIds,
        string storeId,
        bool includePages = true,
        CancellationToken cancellationToken = default);
}
