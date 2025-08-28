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

public class PageBuilderPageSearchService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IPageBuilderPageService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<PageBuilderPageSearchCriteria, PageBuilderPageSearchResult, PageBuilderPage, PageBuilderPageEntity>(
        repositoryFactory, platformMemoryCache, crudService, crudOptions), IPageBuilderPageSearchService
{
    protected override IQueryable<PageBuilderPageEntity> BuildQuery(IRepository repository, PageBuilderPageSearchCriteria criteria)
    {
        var query = ((IPageBuilderModuleRepository)repository).PageBuilderPages;

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        if (!string.IsNullOrEmpty(criteria.Status))
        {
            var statuses = criteria.Status.Split(',', StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(x => statuses.Contains(x.Status));
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(PageBuilderPageSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;

        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(PageBuilderPageEntity.CreatedDate), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(PageBuilderPageEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
