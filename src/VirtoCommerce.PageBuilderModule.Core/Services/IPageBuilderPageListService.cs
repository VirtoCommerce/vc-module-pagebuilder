using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

/// <summary>
/// Pages list where a Multilanguage Page is a single row. The grouped page criteria filter language versions;
/// a Multilanguage Page is listed when any of its language versions matches.
/// </summary>
public interface IPageBuilderPageListService
{
    Task<PageBuilderPageListSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages that are not archived and already serve the permalink in one of the languages: two pages with the same
    /// permalink and language make the storefront pick one of them.
    /// </summary>
    Task<IList<PermalinkUsage>> FindPermalinkUsageAsync(PermalinkUsageRequest request, CancellationToken cancellationToken = default);
}
