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
        await ValidateComponentsAsync(
            repository,
            sharedComponentIds,
            storeId,
            cancellationToken);
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
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in ids.Chunk(QueryBatchSize))
        {
            var pageIds = await repository.PageBuilderSharedComponentReferences
                .Where(x => batch.Contains(x.SharedComponentId))
                .Select(x => x.PageId)
                .Distinct()
                .ToListAsync(cancellationToken);

            result.UnionWith(pageIds);
        }

        return result.ToList();
    }

    private static async Task ValidateComponentsAsync(
        IPageBuilderModuleRepository repository,
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

        PageBuilderSharedComponentReferenceValidator.Validate(
            sharedComponentIds,
            pageStoreId,
            componentStores,
            contentIds);
    }
}
