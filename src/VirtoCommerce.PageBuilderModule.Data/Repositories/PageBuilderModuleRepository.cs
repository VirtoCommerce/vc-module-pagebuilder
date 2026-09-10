using System.Data;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public class PageBuilderModuleRepository : DbContextRepositoryBase<PageBuilderModuleDbContext>, IPageBuilderModuleRepository
{
    private const int GroupQueryBatchSize = 500;

    public PageBuilderModuleRepository(
        PageBuilderModuleDbContext dbContext,
        IUnitOfWork unitOfWork = null)
        : base(dbContext, unitOfWork)
    {
    }

    public IQueryable<PageBuilderPageEntity> PageBuilderPages => DbContext.Set<PageBuilderPageEntity>();

    public IQueryable<PageBuilderContentEntity> PageBuilderContents => DbContext.Set<PageBuilderContentEntity>();

    public IQueryable<GroupedPageBuilderPageEntity> GroupedPageBuilderPages => DbContext.Set<GroupedPageBuilderPageEntity>();

    public IQueryable<PageBuilderAssetReferenceEntity> PageBuilderAssetReferences => DbContext.Set<PageBuilderAssetReferenceEntity>();

    public IQueryable<PageBuilderSharedComponentEntity> PageBuilderSharedComponents => DbContext.Set<PageBuilderSharedComponentEntity>();

    public IQueryable<PageBuilderSharedComponentContentEntity> PageBuilderSharedComponentContents => DbContext.Set<PageBuilderSharedComponentContentEntity>();

    public IQueryable<PageBuilderSharedComponentReferenceEntity> PageBuilderSharedComponentReferences => DbContext.Set<PageBuilderSharedComponentReferenceEntity>();

    public IQueryable<PageBuilderSharedComponentAssetReferenceEntity> PageBuilderSharedComponentAssetReferences => DbContext.Set<PageBuilderSharedComponentAssetReferenceEntity>();

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

    public virtual async Task<IList<PageBuilderSharedComponentEntity>> GetPageBuilderSharedComponentsByIdsAsync(
        IList<string> ids,
        string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return ids.Count == 1
            ? await PageBuilderSharedComponents.Where(x => x.Id == ids.First()).ToListAsync()
            : await PageBuilderSharedComponents.Where(x => ids.Contains(x.Id)).ToListAsync();
    }

    public virtual Task<bool> ExecuteUnderSharedComponentWriteLockAsync(
        string sharedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedComponentId);
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteUnderSharedComponentWriteLockCoreAsync(
            sharedComponentId,
            operation,
            cancellationToken);
    }

    public virtual Task<bool> ExecuteUnderSharedComponentWriteLocksAsync(
        IEnumerable<string> sharedComponentIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteUnderSharedComponentWriteLocksCoreAsync(
            sharedComponentIds,
            operation,
            cancellationToken);
    }

    public virtual Task ExecuteUnderPageWriteLocksAsync(
        IEnumerable<string> pageIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteUnderPageWriteLocksCoreAsync(pageIds, operation, cancellationToken);
    }

    public virtual Task ExecuteUnderGroupedPageWriteLocksAsync(
        IEnumerable<string> groupIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteUnderGroupedPageWriteLocksCoreAsync(groupIds, operation, cancellationToken);
    }

    public virtual Task RebuildPageAssetReferenceIndexAsync(
        string pageId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteUnderPageWriteLocksAsync(
            [pageId],
            transactionCancellationToken =>
                PageBuilderPageIndexing.RebuildCurrentRawPageAssetIndexAsync(
                    DbContext,
                    pageId,
                    transactionCancellationToken),
            cancellationToken);
    }

    private async Task<bool> ExecuteUnderSharedComponentWriteLockCoreAsync(
        string sharedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction != null)
        {
            return await ExecuteUnderSharedComponentWriteLockInternalAsync(
                sharedComponentId,
                operation,
                cancellationToken);
        }

        await using var transaction = await DbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var result = await ExecuteUnderSharedComponentWriteLockInternalAsync(
            sharedComponentId,
            operation,
            cancellationToken);

        if (result)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private async Task<bool> ExecuteUnderSharedComponentWriteLockInternalAsync(
        string sharedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        if (!await PageBuilderWriteLock.AcquireSharedComponentLockAsync(
                DbContext,
                sharedComponentId,
                cancellationToken))
        {
            return false;
        }

        await operation(cancellationToken);
        return true;
    }

    private async Task<bool> ExecuteUnderSharedComponentWriteLocksCoreAsync(
        IEnumerable<string> sharedComponentIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        var orderedIds = PageBuilderWriteLock.OrderIds(sharedComponentIds);

        if (DbContext.Database.CurrentTransaction != null)
        {
            return await ExecuteUnderSharedComponentWriteLocksInternalAsync(
                orderedIds,
                operation,
                cancellationToken);
        }

        await using var transaction = await DbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var result = await ExecuteUnderSharedComponentWriteLocksInternalAsync(
            orderedIds,
            operation,
            cancellationToken);
        if (result)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private async Task<bool> ExecuteUnderSharedComponentWriteLocksInternalAsync(
        IEnumerable<string> sharedComponentIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        foreach (var sharedComponentId in sharedComponentIds)
        {
            if (!await PageBuilderWriteLock.AcquireSharedComponentLockAsync(
                    DbContext,
                    sharedComponentId,
                    cancellationToken))
            {
                return false;
            }
        }

        await operation(cancellationToken);
        return true;
    }

    private async Task ExecuteUnderPageWriteLocksCoreAsync(
        IEnumerable<string> pageIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction != null)
        {
            await PageBuilderWriteLock.AcquirePageLocksAsync(
                DbContext,
                pageIds,
                modifiedDate: null,
                cancellationToken);
            await operation(cancellationToken);
            return;
        }

        await using var transaction = await DbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await PageBuilderWriteLock.AcquirePageLocksAsync(
            DbContext,
            pageIds,
            modifiedDate: null,
            cancellationToken);
        await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ExecuteUnderGroupedPageWriteLocksCoreAsync(
        IEnumerable<string> groupIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction != null)
        {
            await ExecuteUnderGroupedPageWriteLocksInternalAsync(groupIds, operation, cancellationToken);
            return;
        }

        await using var transaction = await DbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await ExecuteUnderGroupedPageWriteLocksInternalAsync(groupIds, operation, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ExecuteUnderGroupedPageWriteLocksInternalAsync(
        IEnumerable<string> groupIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        // Lock groups first so concurrent aggregate saves cannot change page membership between discovering
        // the children and locking them. All group saves use this order; content writers lock only their page
        // (then components), so the ordering cannot form a cycle.
        var orderedGroupIds = PageBuilderWriteLock.OrderIds(groupIds);
        await PageBuilderWriteLock.AcquireGroupedPageLocksAsync(
            DbContext,
            orderedGroupIds,
            cancellationToken);

        var pageIds = new List<string>();
        foreach (var batch in orderedGroupIds.Chunk(GroupQueryBatchSize))
        {
            pageIds.AddRange(await PageBuilderPages
                .Where(x => batch.Contains(x.GroupId))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken));
        }

        await PageBuilderWriteLock.AcquirePageLocksAsync(
            DbContext,
            pageIds,
            modifiedDate: null,
            cancellationToken);
        await operation(cancellationToken);
    }

}
