using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderLinkedComponentReferenceIndexService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderLinkedComponentReferenceIndexService
{
    private const int QueryBatchSize = 500;

    public async Task ValidateReferencesForStoreAsync(
        string storeId,
        IEnumerable<string> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);

        var linkedComponentIds = PageBuilderWriteLock.OrderIds(
            (contents ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(PageBuilderLinkedComponentReferenceMatcher.ExtractReferences));

        if (linkedComponentIds.Length == 0)
        {
            return;
        }

        using var repository = repositoryFactory();
        var linkedRepository = repository.RequireLinkedComponents();
        await ValidateComponentsAsync(
            linkedRepository,
            linkedComponentIds,
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

        var linkedComponentIds = PageBuilderLinkedComponentReferenceMatcher
            .ExtractReferences(content)
            .ToArray();

        using var repository = repositoryFactory();
        var linkedRepository = repository.RequireLinkedComponents();
        var pageStoreId = await GetPageStoreIdAsync(linkedRepository, pageId, cancellationToken);

        var existingReferences = await linkedRepository.PageBuilderLinkedComponentReferences
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

        if (linkedComponentIds.Length > 0)
        {
            await ValidateComponentsAsync(linkedRepository, linkedComponentIds, pageStoreId, cancellationToken);

            foreach (var linkedComponentId in linkedComponentIds)
            {
                repository.Add(new PageBuilderLinkedComponentReferenceEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    PageId = pageId,
                    LinkedComponentId = linkedComponentId,
                });
            }
        }

        await repository.UnitOfWork.CommitAsync();
    }

    public async Task<IList<string>> GetPageIdsAsync(
        IEnumerable<string> linkedComponentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = linkedComponentIds
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        using var repository = repositoryFactory();
        var linkedRepository = repository.RequireLinkedComponents();
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in ids.Chunk(QueryBatchSize))
        {
            var pageIds = await linkedRepository.PageBuilderLinkedComponentReferences
                .Where(x => batch.Contains(x.LinkedComponentId))
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
        IEnumerable<string> linkedComponentIds,
        CancellationToken cancellationToken = default)
    {
        var existingReferences = await dbContext.Set<PageBuilderLinkedComponentReferenceEntity>()
            .Where(x => x.PageId == pageId)
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(existingReferences);

        await dbContext.AddRangeAsync(
            linkedComponentIds.Select(linkedComponentId => new PageBuilderLinkedComponentReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                PageId = pageId,
                LinkedComponentId = linkedComponentId,
            }),
            cancellationToken);
    }

    private static Task<string> GetPageStoreIdAsync(
        IPageBuilderLinkedComponentRepository repository,
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
        IPageBuilderLinkedComponentRepository repository,
        string[] linkedComponentIds,
        string pageStoreId,
        CancellationToken cancellationToken)
    {
        await ValidateComponentsAsync(
            repository.PageBuilderLinkedComponents,
            repository.PageBuilderLinkedComponentContents,
            linkedComponentIds,
            pageStoreId,
            cancellationToken);
    }

    private static async Task ValidateComponentsAsync(
        IQueryable<PageBuilderLinkedComponentEntity> componentsQuery,
        IQueryable<PageBuilderLinkedComponentContentEntity> contentsQuery,
        string[] linkedComponentIds,
        string pageStoreId,
        CancellationToken cancellationToken)
    {
        var componentStores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var contentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in linkedComponentIds.Chunk(QueryBatchSize))
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
            linkedComponentIds,
            pageStoreId,
            componentStores,
            contentIds);
    }

    internal static void ValidateComponentReferences(
        IEnumerable<string> linkedComponentIds,
        string pageStoreId,
        IReadOnlyDictionary<string, string> componentStores,
        ISet<string> contentIds)
    {
        foreach (var linkedComponentId in linkedComponentIds)
        {
            if (!componentStores.TryGetValue(linkedComponentId, out var componentStoreId))
            {
                throw new InvalidDataException($"Linked Component '{linkedComponentId}' was not found.");
            }

            if (!contentIds.Contains(linkedComponentId))
            {
                throw new InvalidDataException($"Linked Component '{linkedComponentId}' has no content.");
            }

            if (!string.Equals(componentStoreId, pageStoreId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Linked Component '{linkedComponentId}' belongs to a different store and cannot be inserted into this page.");
            }
        }
    }
}
