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

        if (!string.IsNullOrEmpty(criteria.Statuses))
        {
            var statuses = criteria.Statuses.Split(',', StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(x => statuses.Contains(x.Status));
        }

        if (criteria.ObjectIds is { Count: > 0 })
        {
            query = query.Where(x => criteria.ObjectIds.Contains(x.Id));
        }

        if (criteria.ModifiedSince.HasValue)
        {
            query = query.Where(x => x.ModifiedDate >= criteria.ModifiedSince.Value || x.CreatedDate >= criteria.ModifiedSince.Value);
        }

        if (criteria.ModifiedBefore.HasValue)
        {
            query = query.Where(x => x.ModifiedDate <= criteria.ModifiedBefore.Value || (x.ModifiedDate == null && x.CreatedDate <= criteria.ModifiedBefore.Value));
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
