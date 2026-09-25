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
        var pageBuilderRepository = (IPageBuilderModuleRepository)repository;
        var query = pageBuilderRepository.PageBuilderPages;

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

        return ApplyDateRange(
            query,
            pageBuilderRepository,
            criteria.ModifiedSince,
            criteria.ModifiedBefore);
    }

    private static IQueryable<PageBuilderPageEntity> ApplyDateRange(
        IQueryable<PageBuilderPageEntity> pages,
        IPageBuilderModuleRepository repository,
        DateTime? modifiedSince,
        DateTime? modifiedBefore)
    {
        if (modifiedSince.HasValue)
        {
            var start = modifiedSince.Value;
            pages = pages.Where(page =>
                (page.ModifiedDate ?? page.CreatedDate) >= start ||
                repository.PageBuilderSharedComponentReferences
                    .Where(reference => reference.PageId == page.Id)
                    .Join(
                        repository.PageBuilderSharedComponents,
                        reference => reference.SharedComponentId,
                        component => component.Id,
                        (_, component) => component.ModifiedDate ?? component.CreatedDate)
                    .Any(changeDate => changeDate >= start));
        }

        if (modifiedBefore.HasValue)
        {
            var end = modifiedBefore.Value;
            pages = pages.Where(page =>
                (page.ModifiedDate ?? page.CreatedDate) <= end &&
                !repository.PageBuilderSharedComponentReferences
                    .Where(reference => reference.PageId == page.Id)
                    .Join(
                        repository.PageBuilderSharedComponents,
                        reference => reference.SharedComponentId,
                        component => component.Id,
                        (_, component) => component.ModifiedDate ?? component.CreatedDate)
                    .Any(changeDate => changeDate > end));
        }

        return pages;
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
