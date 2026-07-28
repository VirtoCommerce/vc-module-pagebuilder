using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderModuleRepository : IRepository
{
    IQueryable<PageBuilderPageEntity> PageBuilderPages { get; }
    IQueryable<GroupedPageBuilderPageEntity> GroupedPageBuilderPages { get; }
    IQueryable<PageBuilderAssetReferenceEntity> PageBuilderAssetReferences { get; }
    IQueryable<PageBuilderLinkedComponentEntity> PageBuilderLinkedComponents { get; }
    IQueryable<PageBuilderLinkedComponentContentEntity> PageBuilderLinkedComponentContents { get; }
    IQueryable<PageBuilderLinkedComponentReferenceEntity> PageBuilderLinkedComponentReferences { get; }
    IQueryable<PageBuilderLinkedComponentAssetReferenceEntity> PageBuilderLinkedComponentAssetReferences { get; }

    Task<IList<PageBuilderPageEntity>> GetPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup);
    Task<IList<GroupedPageBuilderPageEntity>> GetGroupedPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup);
    Task<IList<PageBuilderLinkedComponentEntity>> GetPageBuilderLinkedComponentsByIdsAsync(IList<string> ids, string responseGroup);

    Task<bool> ExecuteUnderLinkedComponentWriteLockAsync(
        string linkedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
