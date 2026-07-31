using VirtoCommerce.PageBuilderModule.Data.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderLinkedComponentRepository : IPageBuilderModuleRepository
{
    IQueryable<PageBuilderLinkedComponentEntity> PageBuilderLinkedComponents { get; }
    IQueryable<PageBuilderLinkedComponentContentEntity> PageBuilderLinkedComponentContents { get; }
    IQueryable<PageBuilderLinkedComponentReferenceEntity> PageBuilderLinkedComponentReferences { get; }
    IQueryable<PageBuilderLinkedComponentAssetReferenceEntity> PageBuilderLinkedComponentAssetReferences { get; }

    Task<IList<PageBuilderLinkedComponentEntity>> GetPageBuilderLinkedComponentsByIdsAsync(
        IList<string> ids,
        string responseGroup);
}
