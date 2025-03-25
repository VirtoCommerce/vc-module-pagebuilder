using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Data.Services
{
    public class GroupedPageSearchService : SearchService<PageBuilderPageSearchCriteria, GroupedPageBuilderPageSearchResult, GroupedPageBuilderPage, GroupedPageBuilderPageEntity>, IGroupedPageSearchService
    {
        private Func<IPageBuilderModuleRepository> _repositoryFactory;

        public GroupedPageSearchService(
            Func<IPageBuilderModuleRepository> repositoryFactory,
            IPlatformMemoryCache platformMemoryCache,
            IGroupedPageService crudService,
            IOptions<CrudOptions> crudOptions)
            : base(repositoryFactory, platformMemoryCache, crudService, crudOptions)
        {
            _repositoryFactory = repositoryFactory;
        }

        protected override IQueryable<GroupedPageBuilderPageEntity> BuildQuery(IRepository repository, PageBuilderPageSearchCriteria criteria)
        {
            var query = ((IPageBuilderModuleRepository)repository).GroupedPageBuilderPages;

            if (!string.IsNullOrEmpty(criteria.StoreId))
            {
                query = query.Where(x => x.StoreId == criteria.StoreId);
            }

            if (!string.IsNullOrEmpty(criteria.Status))
            {
                query = query.Where(x => x.Status == criteria.Status);
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
