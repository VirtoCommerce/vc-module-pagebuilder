using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderPageSearchService : ISearchService<PageBuilderPageSearchCriteria, PageBuilderPageSearchResult, PageBuilderPage>
{
}
