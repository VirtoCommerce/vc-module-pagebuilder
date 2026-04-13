using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.Services
{
    public class GroupedPageSearchService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IGroupedPageService crudService,
        IOptions<CrudOptions> crudOptions)
        : SearchService<PageBuilderPageSearchCriteria, GroupedPageBuilderPageSearchResult, GroupedPageBuilderPage,
                GroupedPageBuilderPageEntity>(repositoryFactory, platformMemoryCache, crudService, crudOptions),
            IGroupedPageSearchService
    {
        protected override IQueryable<GroupedPageBuilderPageEntity> BuildQuery(IRepository repository, PageBuilderPageSearchCriteria criteria)
        {
            var query = ((IPageBuilderModuleRepository)repository).GroupedPageBuilderPages;

            if (!string.IsNullOrEmpty(criteria.StoreId))
            {
                query = query.Where(x => x.StoreId == criteria.StoreId);
            }

            if (!criteria.Keyword.IsNullOrEmpty())
            {
                query = query.Where(x => x.Name.Contains(criteria.Keyword) || x.Permalink.Contains(criteria.Keyword));
            }

            query = ApplyStatusFilter(query, criteria);
            query = ApplyActiveOnFilter(query, criteria);
            query = ApplyLifecycleFilter(query, criteria);

            if (!string.IsNullOrEmpty(criteria.LanguageCode))
            {
                query = query.Where(x => x.CultureName == criteria.LanguageCode);
            }

            return query;
        }

        protected virtual IQueryable<GroupedPageBuilderPageEntity> ApplyStatusFilter(IQueryable<GroupedPageBuilderPageEntity> query, PageBuilderPageSearchCriteria criteria)
        {
            if (!string.IsNullOrEmpty(criteria.Statuses))
            {
                var statuses = criteria.Statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (statuses.Contains(Archived))
                {
                    // group is archived when all pages are archived
                    // so, we need to split the statuses and check if there are other statuses except "archived"
                    var withoutArchive = statuses.Where(x => x != Archived).ToArray();
                    query = query
                        .Where(x =>
                            x.Pages.All(p => p.Status == Archived) ||
                            x.Pages.Any(p => withoutArchive.Contains(p.Status))
                        );
                }
                else
                {
                    query = query.Where(x => x.Pages.Any(p => statuses.Contains(p.Status)));
                }
            }

            return query;
        }

        protected virtual IQueryable<GroupedPageBuilderPageEntity> ApplyActiveOnFilter(IQueryable<GroupedPageBuilderPageEntity> query, PageBuilderPageSearchCriteria criteria)
        {
            if (criteria.ActiveOn.HasValue && criteria.Lifecycle.IsNullOrEmpty())
            {
                var date = criteria.ActiveOn.Value;
                query = query.Where(x =>
                    (x.StartDate == null || x.StartDate <= date) &&
                    (x.EndDate == null || x.EndDate >= date));
            }

            return query;
        }

        protected virtual IQueryable<GroupedPageBuilderPageEntity> ApplyLifecycleFilter(IQueryable<GroupedPageBuilderPageEntity> query, PageBuilderPageSearchCriteria criteria)
        {
            if (!criteria.Lifecycle.IsNullOrEmpty())
            {
                query = ApplyLifecycleQuery(query, criteria);
            }

            if (criteria.ObjectIds is { Count: > 0 })
            {
                query = query.Where(x => criteria.ObjectIds.Contains(x.Id));
            }

            return query;
        }

        private static IQueryable<GroupedPageBuilderPageEntity> ApplyLifecycleQuery(IQueryable<GroupedPageBuilderPageEntity> query, PageBuilderPageSearchCriteria criteria)
        {
            var now = criteria.ActiveOn ?? DateTime.UtcNow;

            return criteria.Lifecycle switch
            {
                PageLifecycleFilters.Drafts => query.Where(g => g.Pages.Any(p => p.Status == Draft)),
                PageLifecycleFilters.Pending => query.Where(g =>
                    g.Pages.Any(p => p.Status == Published) &&
                    g.StartDate != null &&
                    g.StartDate > now),
                PageLifecycleFilters.Active => query
                    .Where(g => g.Pages.Any(p => p.Status == Published))
                    .Where(g => (g.StartDate == null || g.StartDate <= now) &&
                                (g.EndDate == null || g.EndDate >= now)),
                PageLifecycleFilters.Archived => query.Where(g =>
                    g.Pages.All(p => p.Status == Archived) ||
                    (g.EndDate != null && g.EndDate < now)),
                _ => query,
            };
        }



        protected override IList<SortInfo> BuildSortExpression(PageBuilderPageSearchCriteria criteria)
        {
            var sortInfos = criteria.SortInfos;

            if (sortInfos.IsNullOrEmpty())
            {
                sortInfos =
                [
                    new SortInfo { SortColumn = nameof(GroupedPageBuilderPageEntity.CreatedDate), SortDirection = SortDirection.Descending },
                    new SortInfo { SortColumn = nameof(GroupedPageBuilderPageEntity.Id) },
                ];
            }

            return sortInfos;
        }
    }
}
