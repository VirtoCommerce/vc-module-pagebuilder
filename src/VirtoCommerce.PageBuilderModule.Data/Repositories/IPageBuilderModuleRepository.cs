using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderModuleRepository : IRepository
{
    IQueryable<PageBuilderPageEntity> PageBuilderPages { get; }
    IQueryable<PageBuilderContentEntity> PageBuilderContents { get; }
    IQueryable<GroupedPageBuilderPageEntity> GroupedPageBuilderPages { get; }
    IQueryable<PageBuilderAssetReferenceEntity> PageBuilderAssetReferences { get; }
    IQueryable<PageBuilderSharedComponentEntity> PageBuilderSharedComponents { get; }
    IQueryable<PageBuilderSharedComponentContentEntity> PageBuilderSharedComponentContents { get; }
    IQueryable<PageBuilderSharedComponentReferenceEntity> PageBuilderSharedComponentReferences { get; }
    IQueryable<PageBuilderSharedComponentAssetReferenceEntity> PageBuilderSharedComponentAssetReferences { get; }

    Task<IList<PageBuilderPageEntity>> GetPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup);
    Task<IList<GroupedPageBuilderPageEntity>> GetGroupedPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup);
    Task<IList<PageBuilderSharedComponentEntity>> GetPageBuilderSharedComponentsByIdsAsync(
        IList<string> ids,
        string responseGroup);

    Task<bool> ExecuteUnderSharedComponentWriteLockAsync(
        string sharedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<bool> ExecuteUnderSharedComponentWriteLocksAsync(
        IEnumerable<string> sharedComponentIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteUnderPageWriteLocksAsync(
        IEnumerable<string> pageIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteUnderGroupedPageWriteLocksAsync(
        IEnumerable<string> groupIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task RebuildPageAssetReferenceIndexAsync(
        string pageId,
        CancellationToken cancellationToken = default);
}
