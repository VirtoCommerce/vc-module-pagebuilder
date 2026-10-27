using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.Extensions;

public static class PageBuilderModuleRepositoryExtensions
{
    /// <summary>
    /// The first copy number whose permalinks no page of the store uses yet, in any language or status:
    /// a draft copy takes its address too, and publishing it next to another one would make the storefront pick.
    /// </summary>
    public static async Task<int> FindFreeCopyNumberAsync(this IPageBuilderModuleRepository repository, string storeId,
        IList<string> permalinks, CancellationToken cancellationToken = default)
    {
        // Only permalinks of earlier copies can match a copy permalink.
        var taken = await repository.GroupedPageBuilderPages
            .Where(x => x.StoreId == storeId && x.Permalink.Contains("-copy"))
            .Select(x => x.Permalink)
            .Concat(repository.MultilanguagePages
                .Where(x => x.StoreId == storeId && x.Permalink.Contains("-copy"))
                .Select(x => x.Permalink))
            .ToListAsync(cancellationToken);

        return PageBuilderPageCopy.FindFreeNumber(permalinks, taken);
    }
}
