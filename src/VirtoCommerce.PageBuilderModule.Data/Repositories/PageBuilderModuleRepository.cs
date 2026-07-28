using System.Data;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public class PageBuilderModuleRepository(PageBuilderModuleDbContext dbContext, IUnitOfWork unitOfWork = null)
    : DbContextRepositoryBase<PageBuilderModuleDbContext>(dbContext, unitOfWork), IPageBuilderModuleRepository
{
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
        // A provider-neutral no-op UPDATE takes an exclusive row lock until the surrounding transaction
        // commits. This serializes read-replace-write of the component content and its exact asset index
        // across application instances without relying on process-local synchronization.
        var affectedRows = await PageBuilderLinkedComponents
            .Where(x => x.Id == linkedComponentId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ModifiedDate, x => x.ModifiedDate),
                cancellationToken);

        // MySQL can report changed rows instead of matched rows when UseAffectedRows=true. The no-op
        // UPDATE still acquires the lock in that mode, so confirm existence inside the same transaction.
        if (affectedRows == 0 && !await PageBuilderLinkedComponents
                .AnyAsync(x => x.Id == linkedComponentId, cancellationToken))
        {
            return false;
        }

        await operation(cancellationToken);
        return true;
    }
}
