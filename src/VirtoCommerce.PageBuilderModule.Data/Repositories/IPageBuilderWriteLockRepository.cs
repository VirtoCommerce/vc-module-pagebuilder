namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderWriteLockRepository
{
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
        Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteUnderGroupedPageWriteLocksAsync(
        IEnumerable<string> groupIds,
        Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
