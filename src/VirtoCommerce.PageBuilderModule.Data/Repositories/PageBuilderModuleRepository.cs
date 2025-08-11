using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public class PageBuilderModuleRepository : DbContextRepositoryBase<PageBuilderModuleDbContext>, IPageBuilderModuleRepository
{
    public PageBuilderModuleRepository(PageBuilderModuleDbContext dbContext, IUnitOfWork unitOfWork = null)
        : base(dbContext, unitOfWork)
    {
    }

    public IQueryable<PageBuilderPageEntity> PageBuilderPages => DbContext.Set<PageBuilderPageEntity>();

    public IQueryable<GroupedPageBuilderPageEntity> GroupedPageBuilderPages => DbContext.Set<GroupedPageBuilderPageEntity>();

    public virtual async Task<IList<PageBuilderPageEntity>> GetPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return ids.Count == 1
            ? await PageBuilderPages.Where(x => x.Id == ids.First()).ToListAsync()
            : await PageBuilderPages.Where(x => ids.Contains(x.Id)).ToListAsync();
    }

    public virtual async Task<IList<GroupedPageBuilderPageEntity>> GetGroupedPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        // ids = ids.Where(x => x != "2f0813e2-ef57-4055-ad27-ceebeec44e14").ToArray();

        var groups = ids.Count == 1
            ? await GroupedPageBuilderPages.Where(x => x.Id == ids.First()).ToListAsync()
            : await GroupedPageBuilderPages.Where(x => ids.Contains(x.Id)).ToListAsync();

        if (groups.Count > 0)
        {
            var groupIds = groups.Select(x => x.Id).ToArray();
            await PageBuilderPages.Where(x => groupIds.Contains(x.GroupId)).LoadAsync();
        }

        return groups;
    }
}
