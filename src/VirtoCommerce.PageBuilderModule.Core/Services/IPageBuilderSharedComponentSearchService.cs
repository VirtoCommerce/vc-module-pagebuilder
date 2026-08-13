using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentSearchService
    : ISearchService<PageBuilderSharedComponentSearchCriteria, PageBuilderSharedComponentSearchResult, PageBuilderSharedComponent>;
