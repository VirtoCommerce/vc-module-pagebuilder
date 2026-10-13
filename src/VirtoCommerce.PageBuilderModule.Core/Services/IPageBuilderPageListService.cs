using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

/// <summary>
/// Pages list where a Multilanguage Page is a single row. The grouped page criteria filter language versions;
/// a Multilanguage Page is listed when any of its language versions matches.
/// </summary>
public interface IPageBuilderPageListService
{
    Task<PageBuilderPageListSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria, CancellationToken cancellationToken = default);
}
