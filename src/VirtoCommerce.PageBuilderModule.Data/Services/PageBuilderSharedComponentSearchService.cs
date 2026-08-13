using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderSharedComponentSearchService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IPageBuilderSharedComponentService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<PageBuilderSharedComponentSearchCriteria, PageBuilderSharedComponentSearchResult,
        PageBuilderSharedComponent, PageBuilderSharedComponentEntity>(
        repositoryFactory, platformMemoryCache, crudService, crudOptions),
      IPageBuilderSharedComponentSearchService
{
    protected override IQueryable<PageBuilderSharedComponentEntity> BuildQuery(
        IRepository repository,
        PageBuilderSharedComponentSearchCriteria criteria)
    {
        var query = ((IPageBuilderModuleRepository)repository).PageBuilderSharedComponents;

        if (!string.IsNullOrWhiteSpace(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            query = query.Where(x => x.Name.Contains(criteria.Keyword));
        }

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.ObjectIds.Contains(x.Id));
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(PageBuilderSharedComponentSearchCriteria criteria)
    {
        return criteria.SortInfos.IsNullOrEmpty()
            ?
            [
                new SortInfo { SortColumn = nameof(PageBuilderSharedComponentEntity.Name) },
                new SortInfo { SortColumn = nameof(PageBuilderSharedComponentEntity.Id) },
            ]
            : criteria.SortInfos;
    }
}
