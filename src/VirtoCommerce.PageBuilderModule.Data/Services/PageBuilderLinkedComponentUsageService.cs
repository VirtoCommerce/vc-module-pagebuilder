using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderLinkedComponentUsageService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderLinkedComponentUsageService
{
    public async Task<IList<PageBuilderLinkedComponentUsage>> GetUsageAsync(
        IEnumerable<string> linkedComponentIds,
        string storeId,
        bool includePages = true,
        CancellationToken cancellationToken = default)
    {
        var result = CreateUsageResults(linkedComponentIds);

        if (result.Count == 0 || string.IsNullOrWhiteSpace(storeId))
        {
            return result.Values.ToList();
        }

        using var repository = repositoryFactory();
        var usageQuery = CreateUsageQuery(repository, result.Keys.ToArray(), storeId);

        if (includePages)
        {
            await LoadAndApplyUsagePagesAsync(result, usageQuery, cancellationToken);
        }
        else
        {
            await LoadAndApplyUsageCountsAsync(result, usageQuery, cancellationToken);
        }

        return result.Values.ToList();
    }

    private static Dictionary<string, PageBuilderLinkedComponentUsage> CreateUsageResults(
        IEnumerable<string> linkedComponentIds)
    {
        var ids = linkedComponentIds ?? [];

        return ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x,
                x => new PageBuilderLinkedComponentUsage { LinkedComponentId = x },
                StringComparer.OrdinalIgnoreCase);
    }

    private static IQueryable<IndexedUsageResult> CreateUsageQuery(
        IPageBuilderModuleRepository repository,
        string[] linkedComponentIds,
        string storeId)
    {
        return repository.PageBuilderLinkedComponentReferences
            .Where(reference => linkedComponentIds.Contains(reference.LinkedComponentId))
            .Join(
                repository.PageBuilderPages,
                reference => reference.PageId,
                page => page.Id,
                (reference, page) => new { reference, page })
            .Join(
                repository.GroupedPageBuilderPages,
                x => x.page.GroupId,
                group => group.Id,
                (x, group) => new IndexedUsageResult
                {
                    LinkedComponentId = x.reference.LinkedComponentId,
                    GroupId = group.Id,
                    StoreId = x.page.StoreId ?? group.StoreId,
                    Name = group.Name,
                    Permalink = group.Permalink,
                    CultureName = group.CultureName,
                    Status = x.page.Status,
                })
            .Where(x => x.StoreId == storeId);
    }

    private static async Task LoadAndApplyUsageCountsAsync(
        Dictionary<string, PageBuilderLinkedComponentUsage> result,
        IQueryable<IndexedUsageResult> usageQuery,
        CancellationToken cancellationToken)
    {
        var usageCounts = await usageQuery
            .Select(x => new { x.LinkedComponentId, x.GroupId })
            .Distinct()
            .GroupBy(x => x.LinkedComponentId)
            .Select(x => new { LinkedComponentId = x.Key, UsageCount = x.Count() })
            .ToListAsync(cancellationToken);

        foreach (var usageCount in usageCounts)
        {
            if (result.TryGetValue(usageCount.LinkedComponentId, out var componentUsage))
            {
                componentUsage.UsageCount = usageCount.UsageCount;
            }
        }
    }

    private static async Task LoadAndApplyUsagePagesAsync(
        Dictionary<string, PageBuilderLinkedComponentUsage> result,
        IQueryable<IndexedUsageResult> usageQuery,
        CancellationToken cancellationToken)
    {
        var usages = await usageQuery.ToListAsync(cancellationToken);

        foreach (var componentUsages in usages.GroupBy(x => x.LinkedComponentId, StringComparer.OrdinalIgnoreCase))
        {
            if (!result.TryGetValue(componentUsages.Key, out var componentUsage))
            {
                continue;
            }

            componentUsage.Pages = CreateUsagePages(componentUsages);
            componentUsage.UsageCount = componentUsage.Pages.Count;
        }
    }

    private static List<PageBuilderLinkedComponentUsagePage> CreateUsagePages(
        IEnumerable<IndexedUsageResult> usages)
    {
        return usages
            .GroupBy(x => new { x.GroupId, x.Name, x.Permalink, x.CultureName })
            .Select(x => new PageBuilderLinkedComponentUsagePage
            {
                Id = x.Key.GroupId,
                Name = x.Key.Name,
                Permalink = x.Key.Permalink,
                CultureName = x.Key.CultureName,
                Status = string.Join(", ", x.Select(y => y.Status).Distinct().OrderBy(y => y)),
            })
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private sealed class IndexedUsageResult
    {
        public string LinkedComponentId { get; init; }
        public string GroupId { get; init; }
        public string StoreId { get; init; }
        public string Name { get; init; }
        public string Permalink { get; init; }
        public string CultureName { get; init; }
        public string Status { get; init; }
    }
}
