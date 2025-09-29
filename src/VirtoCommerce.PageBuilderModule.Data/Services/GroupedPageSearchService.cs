using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;
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

            if (!string.IsNullOrEmpty(criteria.Statuses))
            {
                var statuses = criteria.Statuses.Split(',', StringSplitOptions.RemoveEmptyEntries);

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

            if (!string.IsNullOrEmpty(criteria.LanguageCode))
            {
                query = query.Where(x => x.CultureName == criteria.LanguageCode);
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
                    new SortInfo { SortColumn = nameof(GroupedPageBuilderPageEntity.CreatedDate), SortDirection = SortDirection.Descending },
                    new SortInfo { SortColumn = nameof(GroupedPageBuilderPageEntity.Id) },
                ];
            }

            return sortInfos;
        }
    }
}
