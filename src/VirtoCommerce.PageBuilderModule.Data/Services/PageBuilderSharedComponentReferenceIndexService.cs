using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderSharedComponentReferenceIndexService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderSharedComponentReferenceIndexService
{
    private const int QueryBatchSize = 500;

    public async Task ValidateReferencesForStoreAsync(
        string storeId,
        IEnumerable<string> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);

        var sharedComponentIds = PageBuilderWriteLock.OrderIds(
            (contents ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(PageBuilderSharedComponentReferenceMatcher.ExtractReferences));

        if (sharedComponentIds.Length == 0)
        {
            return;
        }

        using var repository = repositoryFactory();
        var sharedComponentRepository = repository.RequireSharedComponents();
        await ValidateComponentsAsync(
            sharedComponentRepository,
            sharedComponentIds,
            storeId,
            cancellationToken);
    }

    public async Task RebuildPageIndexAsync(
        string pageId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return;
        }

        var sharedComponentIds = PageBuilderSharedComponentReferenceMatcher
            .ExtractReferences(content)
            .ToArray();

        using var repository = repositoryFactory();
        var sharedComponentRepository = repository.RequireSharedComponents();
        var pageStoreId = await GetPageStoreIdAsync(sharedComponentRepository, pageId, cancellationToken);

        var existingReferences = await sharedComponentRepository.PageBuilderSharedComponentReferences
            .Where(x => x.PageId == pageId)
            .ToListAsync(cancellationToken);

        foreach (var reference in existingReferences)
        {
            repository.Remove(reference);
        }

        if (pageStoreId == null)
        {
            await repository.UnitOfWork.CommitAsync();
            return;
        }

        if (sharedComponentIds.Length > 0)
        {
            await ValidateComponentsAsync(sharedComponentRepository, sharedComponentIds, pageStoreId, cancellationToken);

            foreach (var sharedComponentId in sharedComponentIds)
            {
                repository.Add(new PageBuilderSharedComponentReferenceEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    PageId = pageId,
                    SharedComponentId = sharedComponentId,
                });
            }
        }

        await repository.UnitOfWork.CommitAsync();
    }

    public async Task<IList<string>> GetPageIdsAsync(
        IEnumerable<string> sharedComponentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sharedComponentIds
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        using var repository = repositoryFactory();
        var sharedComponentRepository = repository.RequireSharedComponents();
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in ids.Chunk(QueryBatchSize))
        {
            var pageIds = await sharedComponentRepository.PageBuilderSharedComponentReferences
                .Where(x => batch.Contains(x.SharedComponentId))
                .Select(x => x.PageId)
                .Distinct()
                .ToListAsync(cancellationToken);

            result.UnionWith(pageIds);
        }

        return result.ToList();
    }

    internal static async Task ReplacePageIndexInCurrentUnitOfWorkAsync(
        PageBuilderModuleDbContext dbContext,
        string pageId,
        IEnumerable<string> sharedComponentIds,
        CancellationToken cancellationToken = default)
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

    private static Task<string> GetPageStoreIdAsync(
        IPageBuilderSharedComponentRepository repository,
        string pageId,
        CancellationToken cancellationToken)
    {
        return GetPageStoreIdAsync(
            repository.PageBuilderPages,
            repository.GroupedPageBuilderPages,
            pageId,
            cancellationToken);
    }

    private static Task<string> GetPageStoreIdAsync(
        IQueryable<PageBuilderPageEntity> pages,
        IQueryable<GroupedPageBuilderPageEntity> groupedPages,
        string pageId,
        CancellationToken cancellationToken)
    {
        return pages
            .Where(x => x.Id == pageId)
            .Join(
                groupedPages,
                page => page.GroupId,
                group => group.Id,
                (page, group) => page.StoreId ?? group.StoreId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task ValidateComponentsAsync(
        IPageBuilderSharedComponentRepository repository,
        string[] sharedComponentIds,
        string pageStoreId,
        CancellationToken cancellationToken)
    {
        await ValidateComponentsAsync(
            repository.PageBuilderSharedComponents,
            repository.PageBuilderSharedComponentContents,
            sharedComponentIds,
            pageStoreId,
            cancellationToken);
    }

    private static async Task ValidateComponentsAsync(
        IQueryable<PageBuilderSharedComponentEntity> componentsQuery,
        IQueryable<PageBuilderSharedComponentContentEntity> contentsQuery,
        string[] sharedComponentIds,
        string pageStoreId,
        CancellationToken cancellationToken)
    {
        var componentStores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var contentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in sharedComponentIds.Chunk(QueryBatchSize))
        {
            var components = await componentsQuery
                .Where(x => batch.Contains(x.Id))
                .Select(x => new { x.Id, x.StoreId })
                .ToListAsync(cancellationToken);
            var batchContentIds = await contentsQuery
                .Where(x => batch.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var component in components)
            {
                componentStores[component.Id] = component.StoreId;
            }

            contentIds.UnionWith(batchContentIds);
        }

        ValidateComponentReferences(
            sharedComponentIds,
            pageStoreId,
            componentStores,
            contentIds);
    }

    internal static void ValidateComponentReferences(
        IEnumerable<string> sharedComponentIds,
        string pageStoreId,
        IReadOnlyDictionary<string, string> componentStores,
        ISet<string> contentIds)
    {
        foreach (var sharedComponentId in sharedComponentIds)
        {
            if (!componentStores.TryGetValue(sharedComponentId, out var componentStoreId))
            {
                throw new InvalidDataException($"Shared Component '{sharedComponentId}' was not found.");
            }

            if (!contentIds.Contains(sharedComponentId))
            {
                throw new InvalidDataException($"Shared Component '{sharedComponentId}' has no content.");
            }

            if (!string.Equals(componentStoreId, pageStoreId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Shared Component '{sharedComponentId}' belongs to a different store and cannot be inserted into this page.");
            }
        }
    }
}
