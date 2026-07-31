using System.Data;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public class PageBuilderModuleRepository(PageBuilderModuleDbContext dbContext, IUnitOfWork unitOfWork = null)
    : DbContextRepositoryBase<PageBuilderModuleDbContext>(dbContext, unitOfWork),
      IPageBuilderLinkedComponentRepository,
      IPageBuilderWriteLockRepository
{
    private const int GroupQueryBatchSize = 500;

    public IQueryable<PageBuilderPageEntity> PageBuilderPages => DbContext.Set<PageBuilderPageEntity>();

    public IQueryable<GroupedPageBuilderPageEntity> GroupedPageBuilderPages => DbContext.Set<GroupedPageBuilderPageEntity>();

    public IQueryable<PageBuilderAssetReferenceEntity> PageBuilderAssetReferences => DbContext.Set<PageBuilderAssetReferenceEntity>();

    public IQueryable<PageBuilderLinkedComponentEntity> PageBuilderLinkedComponents => DbContext.Set<PageBuilderLinkedComponentEntity>();

    public IQueryable<PageBuilderLinkedComponentContentEntity> PageBuilderLinkedComponentContents => DbContext.Set<PageBuilderLinkedComponentContentEntity>();

    public IQueryable<PageBuilderLinkedComponentReferenceEntity> PageBuilderLinkedComponentReferences => DbContext.Set<PageBuilderLinkedComponentReferenceEntity>();

    public IQueryable<PageBuilderLinkedComponentAssetReferenceEntity> PageBuilderLinkedComponentAssetReferences => DbContext.Set<PageBuilderLinkedComponentAssetReferenceEntity>();

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

    public virtual async Task<IList<PageBuilderLinkedComponentEntity>> GetPageBuilderLinkedComponentsByIdsAsync(
        IList<string> ids,
        string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return ids.Count == 1
            ? await PageBuilderLinkedComponents.Where(x => x.Id == ids.First()).ToListAsync()
            : await PageBuilderLinkedComponents.Where(x => ids.Contains(x.Id)).ToListAsync();
    }

    public virtual Task<bool> ExecuteUnderLinkedComponentWriteLockAsync(
        string linkedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkedComponentId);
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteUnderLinkedComponentWriteLockCoreAsync(
            linkedComponentId,
            operation,
            cancellationToken);
    }

    public virtual Task<bool> ExecuteUnderLinkedComponentWriteLocksAsync(
        IEnumerable<string> linkedComponentIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteUnderLinkedComponentWriteLocksCoreAsync(
            linkedComponentIds,
            operation,
            cancellationToken);
    }

    public virtual Task ExecuteUnderPageWriteLocksAsync(
        IEnumerable<string> pageIds,
        Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteUnderPageWriteLocksCoreAsync(pageIds, operation, cancellationToken);
    }

    public virtual Task ExecuteUnderGroupedPageWriteLocksAsync(
        IEnumerable<string> groupIds,
        Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteUnderGroupedPageWriteLocksCoreAsync(groupIds, operation, cancellationToken);
    }

    private async Task<bool> ExecuteUnderLinkedComponentWriteLockCoreAsync(
        string linkedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction != null)
        {
            return await ExecuteUnderLinkedComponentWriteLockInternalAsync(
                linkedComponentId,
                operation,
                cancellationToken);
        }

        await using var transaction = await DbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var result = await ExecuteUnderLinkedComponentWriteLockInternalAsync(
            linkedComponentId,
            operation,
            cancellationToken);

        if (result)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private async Task<bool> ExecuteUnderLinkedComponentWriteLockInternalAsync(
        string linkedComponentId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        if (!await PageBuilderWriteLock.AcquireLinkedComponentLockAsync(
                DbContext,
                linkedComponentId,
                cancellationToken))
        {
            return false;
        }

        await operation(cancellationToken);
        return true;
    }

    private async Task<bool> ExecuteUnderLinkedComponentWriteLocksCoreAsync(
        IEnumerable<string> linkedComponentIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        var orderedIds = PageBuilderWriteLock.OrderIds(linkedComponentIds);

        if (DbContext.Database.CurrentTransaction != null)
        {
            return await ExecuteUnderLinkedComponentWriteLocksInternalAsync(
                orderedIds,
                operation,
                cancellationToken);
        }

        await using var transaction = await DbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var result = await ExecuteUnderLinkedComponentWriteLocksInternalAsync(
            orderedIds,
            operation,
            cancellationToken);
        if (result)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private async Task<bool> ExecuteUnderLinkedComponentWriteLocksInternalAsync(
        IEnumerable<string> linkedComponentIds,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        foreach (var linkedComponentId in linkedComponentIds)
        {
            if (!await PageBuilderWriteLock.AcquireLinkedComponentLockAsync(
                    DbContext,
                    linkedComponentId,
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
        Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction != null)
        {
            await PageBuilderWriteLock.AcquirePageLocksAsync(
                DbContext,
                pageIds,
                modifiedDate: null,
                cancellationToken);
            await operation(DbContext, cancellationToken);
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
        await operation(DbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ExecuteUnderGroupedPageWriteLocksCoreAsync(
        IEnumerable<string> groupIds,
        Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
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
        Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
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
        await operation(DbContext, cancellationToken);
    }
}
