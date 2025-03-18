using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.PageBuilderModule.Data.Services
{
    public interface IGroupedPageService
    {
        Task<GroupedPageBuilderPageSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria);
        Task<GroupedPageBuilderPage> GetGroupedAsync(string id);
    }

    public class GroupedPageService : IGroupedPageService
    {
        private readonly Func<IPageBuilderModuleRepository> _repositoryFactory;
        private readonly IPlatformMemoryCache _platformMemoryCache;
        private readonly IPageBuilderPageService _crudService;

        public GroupedPageService(
            Func<IPageBuilderModuleRepository> repositoryFactory,
            IPlatformMemoryCache platformMemoryCache,
            IPageBuilderPageService crudService)
        {
            _repositoryFactory = repositoryFactory;
            _platformMemoryCache = platformMemoryCache;
            _crudService = crudService;
        }

        public async Task<GroupedPageBuilderPage> GetGroupedAsync(string id)
        {
            GroupedPageBuilderPage result = null;

            using (var repository = _repositoryFactory())
            {
                var entities = await LoadEntitiesAsync(repository, [id]);

                var entity = entities.FirstOrDefault();

                if (entity != null)
                {
                    result = entity.ToModel(AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance());
                    result.Pages = await _crudService.GetNoCloneAsync(entity.PagesIds);
                }
            }

            return result;
        }

        protected async Task<IList<GroupedPageBuilderPageEntity>> LoadEntitiesAsync(IPageBuilderModuleRepository repository, IList<string> groupedIds)
        {
            var query = repository.PageBuilderPages;

            var groupedPages = query
                .GroupBy(p => new { p.StoreId, p.Name, p.CultureName, p.Permalink })
                .Where(g => groupedIds.Contains(g.Key.StoreId + ":" + g.Key.Name + ":" + g.Key.CultureName + ":" + g.Key.Permalink))
                .Select(g => new GroupedPageBuilderPageEntity
                {
                    Id = g.Key.StoreId + ":" + g.Key.Name + ":" + g.Key.CultureName + ":" + g.Key.Permalink,
                    StoreId = g.Key.StoreId,
                    Name = g.Key.Name,
                    CultureName = g.Key.CultureName,
                    Permalink = g.Key.Permalink,
                    Status = g.Any(p => p.Status == "Archived") ? "Archived" : g.Any(p => p.Status == "Published") ? "Published" : "Draft",
                    HasChanges = g.Any(p => p.Status == "Draft"), // && g.Any(p => p.Status == "Published") 
                    PagesIds = g.Select(x => x.Id).ToList(),
                    CreatedBy = g.First().CreatedBy,
                    ModifiedBy = g.First().ModifiedBy,
                    CreatedDate = g.OrderByDescending(x => x.CreatedDate).First().CreatedDate,
                    ModifiedDate = g.OrderByDescending(x => x.ModifiedDate).First().ModifiedDate,
                });

            return await groupedPages.ToListAsync();
        }

        public async Task<GroupedPageBuilderPageSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria)
        {
            var cacheKey = CacheKey.With(GetType(), nameof(SearchAsync), criteria.GetCacheKey());
            return await _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async cacheEntry =>
            {
                cacheEntry.AddExpirationToken(GenericSearchCachingRegion<PageBuilderPage>.CreateChangeToken());

                var result = AbstractTypeFactory<GroupedPageBuilderPageSearchResult>.TryCreateInstance();

                using (var repository = _repositoryFactory())
                {
                    repository.DisableChangesTracking();

                    var query = BuildQuery(repository, criteria);

                    result.TotalCount = await query.CountAsync();

                    query = query
                        .Skip(criteria.Skip)
                        .Take(criteria.Take);

                    var results = await query.ToListAsync();

                    result.Results = results
                        .Select(x => x.ToModel(AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance()))
                        .ToList();
                }

                return result;
            });
        }

        protected IQueryable<GroupedPageBuilderPageEntity> BuildQuery(IPageBuilderModuleRepository repository, PageBuilderPageSearchCriteria criteria)
        {
            var query = repository.PageBuilderPages;

            if (!string.IsNullOrEmpty(criteria.StoreId))
            {
                query = query.Where(x => x.StoreId == criteria.StoreId);
            }

            var groupedPages = query
                .GroupBy(p => new { p.StoreId, p.Name, p.CultureName, p.Permalink })
                .Select(g => new GroupedPageBuilderPageEntity
                {
                    Id = g.Key.StoreId + ":" + g.Key.Name + ":" + g.Key.CultureName + ":" + g.Key.Permalink,
                    StoreId = g.Key.StoreId,
                    Name = g.Key.Name,
                    CultureName = g.Key.CultureName,
                    Permalink = g.Key.Permalink,
                    Status = g.Any(p => p.Status == "Archived") ? "Archived" : g.Any(p => p.Status == "Published") ? "Published" : "Draft",
                    HasChanges = g.Any(p => p.Status == "Published") && g.Any(p => p.Status == "Draft"),
                    PagesIds = g.Select(x => x.Id).ToList(),
                    CreatedBy = g.First().CreatedBy,
                    ModifiedBy = g.First().ModifiedBy,
                    CreatedDate = g.OrderByDescending(x => x.CreatedDate).First().CreatedDate,
                    ModifiedDate = g.OrderByDescending(x => x.ModifiedDate).First().ModifiedDate,
                });

            if (!string.IsNullOrEmpty(criteria.Status))
            {
                groupedPages = groupedPages.Where(x => x.Status == criteria.Status);
            }

            return groupedPages;
        }
    }
}
