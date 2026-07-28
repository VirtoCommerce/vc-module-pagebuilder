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

public class PageBuilderLinkedComponentSearchService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IPageBuilderLinkedComponentService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<PageBuilderLinkedComponentSearchCriteria, PageBuilderLinkedComponentSearchResult,
        PageBuilderLinkedComponent, PageBuilderLinkedComponentEntity>(
        repositoryFactory, platformMemoryCache, crudService, crudOptions),
      IPageBuilderLinkedComponentSearchService
{
    protected override IQueryable<PageBuilderLinkedComponentEntity> BuildQuery(
        IRepository repository,
        PageBuilderLinkedComponentSearchCriteria criteria)
    {
        var query = ((IPageBuilderModuleRepository)repository).PageBuilderLinkedComponents;

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

    protected override IList<SortInfo> BuildSortExpression(PageBuilderLinkedComponentSearchCriteria criteria)
    {
        return criteria.SortInfos.IsNullOrEmpty()
            ?
            [
                new SortInfo { SortColumn = nameof(PageBuilderLinkedComponentEntity.Name) },
                new SortInfo { SortColumn = nameof(PageBuilderLinkedComponentEntity.Id) },
            ]
            : criteria.SortInfos;
    }
}
