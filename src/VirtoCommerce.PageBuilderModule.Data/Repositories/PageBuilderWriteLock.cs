using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

internal static class PageBuilderWriteLock
{
    internal static string[] OrderIds(IEnumerable<string> ids)
    {
        return ids
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x, StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    internal static async Task AcquirePageLocksAsync(
        PageBuilderModuleDbContext dbContext,
        IEnumerable<string> pageIds,
        DateTime? modifiedDate,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);

        foreach (var pageId in OrderIds(pageIds))
        {
            if (modifiedDate.HasValue)
            {
                await dbContext.Set<PageBuilderPageEntity>()
                    .Where(x => x.Id == pageId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.ModifiedDate, modifiedDate.Value),
                        cancellationToken);
            }
            else
            {
                await dbContext.Set<PageBuilderPageEntity>()
                    .Where(x => x.Id == pageId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.ModifiedDate, x => x.ModifiedDate),
                        cancellationToken);
            }
        }
    }

    internal static async Task AcquireGroupedPageLocksAsync(
        PageBuilderModuleDbContext dbContext,
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);

        foreach (var groupId in OrderIds(groupIds))
        {
            await dbContext.Set<GroupedPageBuilderPageEntity>()
                .Where(x => x.Id == groupId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.ModifiedDate, x => x.ModifiedDate),
                    cancellationToken);
        }
    }

    internal static async Task<bool> AcquireSharedComponentLockAsync(
        PageBuilderModuleDbContext dbContext,
        string sharedComponentId,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);

        var affectedRows = await dbContext.Set<PageBuilderSharedComponentEntity>()
            .Where(x => x.Id == sharedComponentId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ModifiedDate, x => x.ModifiedDate),
                cancellationToken);

        // MySQL can report changed rows rather than matched rows for a no-op UPDATE. The row is still
        // locked, so use a same-transaction existence check before treating zero as "not found".
        return affectedRows != 0 || await dbContext.Set<PageBuilderSharedComponentEntity>()
            .AnyAsync(x => x.Id == sharedComponentId, cancellationToken);
    }

    internal static async Task AcquireSharedComponentLocksAsync(
        PageBuilderModuleDbContext dbContext,
        IEnumerable<string> sharedComponentIds,
        CancellationToken cancellationToken)
    {
        foreach (var sharedComponentId in OrderIds(sharedComponentIds))
        {
            if (!await AcquireSharedComponentLockAsync(dbContext, sharedComponentId, cancellationToken))
            {
                throw new InvalidDataException($"Shared Component '{sharedComponentId}' was not found.");
            }
        }
    }

    private static void EnsureTransaction(PageBuilderModuleDbContext dbContext)
    {
        if (dbContext.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException("Page Builder write locks must be held inside a database transaction.");
        }
    }
}
