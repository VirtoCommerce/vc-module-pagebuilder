using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

internal static class PageBuilderPageIndexing
{
    private const int QueryBatchSize = 500;

    internal static async Task RebuildAfterRawContentWriteAsync(
        PageBuilderModuleDbContext dbContext,
        string pageId,
        string rawContent,
        string groupStoreId,
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

        var pageStoreId = page.StoreId ?? groupStoreId;
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

        await ReplaceSharedComponentIndexAsync(
            dbContext,
            pageId,
            sharedComponentIds,
            cancellationToken);
        await ReplaceAssetReferenceIndexAsync(
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

        await ReplaceAssetReferenceIndexAsync(
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

        PageBuilderSharedComponentReferenceValidator.Validate(
            sharedComponentIds,
            pageStoreId,
            componentStores,
            contentIds);
    }

    private static async Task ReplaceSharedComponentIndexAsync(
        PageBuilderModuleDbContext dbContext,
        string pageId,
        IEnumerable<string> sharedComponentIds,
        CancellationToken cancellationToken)
    {
        var existingReferences = await dbContext.Set<PageBuilderSharedComponentReferenceEntity>()
            .Where(x => x.PageId == pageId)
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(existingReferences);

        await dbContext.AddRangeAsync(
            sharedComponentIds.Select(sharedComponentId => new PageBuilderSharedComponentReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                PageId = pageId,
                SharedComponentId = sharedComponentId,
            }),
            cancellationToken);
    }

    private static async Task ReplaceAssetReferenceIndexAsync(
        PageBuilderModuleDbContext dbContext,
        string pageId,
        string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return;
        }

        var pageExists = await dbContext.Set<PageBuilderPageEntity>()
            .AnyAsync(x => x.Id == pageId, cancellationToken);
        var existingReferences = await dbContext.Set<PageBuilderAssetReferenceEntity>()
            .Where(x => x.PageId == pageId)
            .ToListAsync(cancellationToken);

        dbContext.RemoveRange(existingReferences);

        if (pageExists)
        {
            await dbContext.AddRangeAsync(
                PageBuilderAssetReferenceMatcher.ExtractReferences(content)
                    .Select(reference => new PageBuilderAssetReferenceEntity
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        PageId = pageId,
                        NormalizedAssetUrl = reference,
                        NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(reference),
                    }),
                cancellationToken);
        }
    }

    private static void EnsureTransaction(PageBuilderModuleDbContext dbContext)
    {
        if (dbContext.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException("Page content and its indexes must share a database transaction.");
        }
    }
}
