namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderWriteLockRepository
{
    Task<bool> ExecuteUnderLinkedComponentWriteLockAsync(
        string linkedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<bool> ExecuteUnderLinkedComponentWriteLocksAsync(
        IEnumerable<string> linkedComponentIds,
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
