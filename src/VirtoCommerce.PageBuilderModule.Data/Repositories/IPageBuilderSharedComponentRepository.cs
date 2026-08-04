using VirtoCommerce.PageBuilderModule.Data.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderSharedComponentRepository : IPageBuilderModuleRepository
{
    IQueryable<PageBuilderSharedComponentEntity> PageBuilderSharedComponents { get; }
    IQueryable<PageBuilderSharedComponentContentEntity> PageBuilderSharedComponentContents { get; }
    IQueryable<PageBuilderSharedComponentReferenceEntity> PageBuilderSharedComponentReferences { get; }
    IQueryable<PageBuilderSharedComponentAssetReferenceEntity> PageBuilderSharedComponentAssetReferences { get; }

    Task<IList<PageBuilderSharedComponentEntity>> GetPageBuilderSharedComponentsByIdsAsync(
        IList<string> ids,
        string responseGroup);
}
