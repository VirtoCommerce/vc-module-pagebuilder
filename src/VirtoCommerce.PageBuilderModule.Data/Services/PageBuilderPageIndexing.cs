using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

internal static class PageBuilderPageIndexing
{
    private const int QueryBatchSize = 500;

    internal static async Task RebuildAfterRawContentWriteAsync(
        PageBuilderModuleDbContext dbContext,
        string pageId,
        string rawContent,
        string fallbackStoreId,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);

        var page = await dbContext.Set<PageBuilderPageEntity>()
            .Where(x => x.Id == pageId)
            .Select(x => new { x.StoreId })
            .FirstOrDefaultAsync(cancellationToken);

        if (page == null)
        {
            throw new KeyNotFoundException($"Page '{pageId}' was not found.");
        }

        var pageStoreId = page.StoreId ?? fallbackStoreId;
        if (string.IsNullOrWhiteSpace(pageStoreId))
        {
            throw new InvalidDataException($"Page '{pageId}' has no store.");
        }

        var sharedComponentIds = PageBuilderWriteLock.OrderIds(
            PageBuilderSharedComponentReferenceMatcher.ExtractReferences(rawContent));

        await PageBuilderWriteLock.AcquireSharedComponentLocksAsync(
            dbContext,
            sharedComponentIds,
            cancellationToken);
        await ValidateLockedComponentsAsync(
            dbContext,
            sharedComponentIds,
            pageStoreId,
            cancellationToken);

        await PageBuilderSharedComponentReferenceIndexService.ReplacePageIndexInCurrentUnitOfWorkAsync(
            dbContext,
            pageId,
            sharedComponentIds,
            cancellationToken);
        await PageBuilderAssetReferenceIndexService.ReplacePageIndexInCurrentUnitOfWorkAsync(
            dbContext,
            pageId,
            rawContent,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static async Task RebuildCurrentRawPageAssetIndexAsync(
        PageBuilderModuleDbContext dbContext,
        string pageId,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);

        var rawContent = await dbContext.Set<PageBuilderContentEntity>()
            .AsNoTracking()
            .Where(x => x.Id == pageId)
            .Select(x => x.PageContent)
            .FirstOrDefaultAsync(cancellationToken);

        await PageBuilderAssetReferenceIndexService.ReplacePageIndexInCurrentUnitOfWorkAsync(
            dbContext,
            pageId,
            rawContent,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ValidateLockedComponentsAsync(
        PageBuilderModuleDbContext dbContext,
        string[] sharedComponentIds,
        string pageStoreId,
        CancellationToken cancellationToken)
    {
        if (sharedComponentIds.Length == 0)
        {
            return;
        }

        var componentStores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var contentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in sharedComponentIds.Chunk(QueryBatchSize))
        {
            var stores = await dbContext.Set<PageBuilderSharedComponentEntity>()
                .Where(x => batch.Contains(x.Id))
                .Select(x => new { x.Id, x.StoreId })
                .ToListAsync(cancellationToken);
            var batchContentIds = await dbContext.Set<PageBuilderSharedComponentContentEntity>()
                .Where(x => batch.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var component in stores)
            {
                componentStores[component.Id] = component.StoreId;
            }

            contentIds.UnionWith(batchContentIds);
        }

        PageBuilderSharedComponentReferenceIndexService.ValidateComponentReferences(
            sharedComponentIds,
            pageStoreId,
            componentStores,
            contentIds);
    }

    private static void EnsureTransaction(PageBuilderModuleDbContext dbContext)
    {
        if (dbContext.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException("Page content and its indexes must share a database transaction.");
        }
    }
}
