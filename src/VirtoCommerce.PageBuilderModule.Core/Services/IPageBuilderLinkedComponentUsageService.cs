using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderLinkedComponentUsageService
{
    Task<IList<PageBuilderLinkedComponentUsage>> GetUsageAsync(
        IEnumerable<string> linkedComponentIds,
        string storeId,
        bool includePages = true,
        CancellationToken cancellationToken = default);
}
