using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderModuleRepository : IRepository
{
    IQueryable<PageBuilderPageEntity> PageBuilderPages { get; }
    IQueryable<GroupedPageBuilderPageEntity> GroupedPageBuilderPages { get; }
    IQueryable<PageBuilderAssetReferenceEntity> PageBuilderAssetReferences { get; }

    Task<IList<PageBuilderPageEntity>> GetPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup);
    Task<IList<GroupedPageBuilderPageEntity>> GetGroupedPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup);
}
