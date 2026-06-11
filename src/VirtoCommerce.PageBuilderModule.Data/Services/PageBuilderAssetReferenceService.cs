using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetReferenceService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderAssetReferenceService
{
    public Task<PageBuilderAssetReferencesSearchResult> SearchReferencesAsync(PageBuilderAssetReferencesSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return SearchReferencesInternalAsync(criteria, cancellationToken);
    }

    private async Task<PageBuilderAssetReferencesSearchResult> SearchReferencesInternalAsync(PageBuilderAssetReferencesSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var references = CreateReferences(criteria.AssetUrls);

        if (references.Count == 0 || string.IsNullOrWhiteSpace(criteria.StoreId))
        {
            return CreateResult(references.Values);
        }

        using var repository = repositoryFactory();
        var indexedReferences = CreateReferencesQuery(repository, criteria, references.Keys);
        var referenceGroups = await LoadReferenceGroupsAsync(indexedReferences, cancellationToken);

        ApplyReferenceCounts(references, referenceGroups);

        if (criteria.IncludePages)
        {
            var referencePages = await LoadReferencePagesAsync(repository, indexedReferences, cancellationToken);
            ApplyReferencePages(references, referencePages);
        }

        return CreateResult(references.Values);
    }

    private static Dictionary<string, PageBuilderAssetReference> CreateReferences(IEnumerable<string> assetUrls)
    {
        var normalizedAssets = PageBuilderAssetReferenceMatcher.NormalizeAssetUrls(assetUrls ?? []);

        return normalizedAssets.ToDictionary(
            x => x.Key,
            x => new PageBuilderAssetReference
            {
                AssetUrl = x.Value,
                NormalizedAssetUrl = x.Key,
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private static IQueryable<PageBuilderAssetReferenceEntity> CreateReferencesQuery(
        IPageBuilderModuleRepository repository,
        PageBuilderAssetReferencesSearchCriteria criteria,
        IEnumerable<string> normalizedAssetUrls)
    {
        var normalizedAssetUrlHashes = normalizedAssetUrls
            .Select(PageBuilderAssetReferenceMatcher.GetAssetUrlHash)
            .ToArray();
        var allowedStatuses = GetAllowedStatuses(criteria.Statuses).ToArray();

        var query = repository.PageBuilderAssetReferences
            .Where(x => x.StoreId == criteria.StoreId)
            .Where(x => normalizedAssetUrlHashes.Contains(x.NormalizedAssetUrlHash))
            .Where(x => allowedStatuses.Contains(x.Status));

        if (!string.IsNullOrWhiteSpace(criteria.LanguageCode))
        {
            query = query.Where(x => x.CultureName == criteria.LanguageCode);
        }

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.ObjectIds.Contains(x.GroupId));
        }

        return query;
    }

    private static Task<List<ReferenceCountResult>> LoadReferenceGroupsAsync(
        IQueryable<PageBuilderAssetReferenceEntity> indexedReferences,
        CancellationToken cancellationToken)
    {
        return indexedReferences
            .GroupBy(x => x.NormalizedAssetUrl)
            .Select(x => new ReferenceCountResult
            {
                NormalizedAssetUrl = x.Key,
                ReferencesCount = x.Select(r => r.GroupId).Distinct().Count(),
            })
            .ToListAsync(cancellationToken);
    }

    private static void ApplyReferenceCounts(
        IDictionary<string, PageBuilderAssetReference> references,
        IEnumerable<ReferenceCountResult> referenceGroups)
    {
        foreach (var group in referenceGroups)
        {
            if (references.TryGetValue(group.NormalizedAssetUrl, out var reference))
            {
                reference.ReferencesCount = group.ReferencesCount;
            }
        }
    }

    private static Task<List<ReferencePageResult>> LoadReferencePagesAsync(
        IPageBuilderModuleRepository repository,
        IQueryable<PageBuilderAssetReferenceEntity> indexedReferences,
        CancellationToken cancellationToken)
    {
        return indexedReferences
            .Join(
                repository.GroupedPageBuilderPages,
                reference => reference.GroupId,
                group => group.Id,
                (reference, group) => new ReferencePageResult
                {
                    NormalizedAssetUrl = reference.NormalizedAssetUrl,
                    Id = group.Id,
                    Name = group.Name,
                    Permalink = group.Permalink,
                    CultureName = group.CultureName,
                    Status = reference.Status,
                })
            .ToListAsync(cancellationToken);
    }

    private static void ApplyReferencePages(
        IDictionary<string, PageBuilderAssetReference> references,
        IEnumerable<ReferencePageResult> referencePages)
    {
        foreach (var referencePage in referencePages
            .GroupBy(x => new { x.NormalizedAssetUrl, x.Id, x.Name, x.Permalink, x.CultureName }))
        {
            if (!references.TryGetValue(referencePage.Key.NormalizedAssetUrl, out var reference))
            {
                continue;
            }

            reference.Pages.Add(new PageBuilderAssetReferencePage
            {
                Id = referencePage.Key.Id,
                Name = referencePage.Key.Name,
                Permalink = referencePage.Key.Permalink,
                CultureName = referencePage.Key.CultureName,
                Status = string.Join(", ", referencePage
                    .Select(x => x.Status)
                    .Distinct()
                    .OrderBy(x => x)),
            });
        }
    }

    private static PageBuilderAssetReferencesSearchResult CreateResult(IEnumerable<PageBuilderAssetReference> references)
    {
        var results = references.ToList();

        return new PageBuilderAssetReferencesSearchResult
        {
            TotalCount = results.Count,
            Results = results,
        };
    }

    private static IEnumerable<PageBuilderPageStatus> GetAllowedStatuses(string statuses)
    {
        return GetStatuses(statuses)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Enum.TryParse<PageBuilderPageStatus>(x, ignoreCase: true, out var status)
                ? status
                : (PageBuilderPageStatus?)null)
            .Where(x => x.HasValue)
            .Select(x => x.Value);
    }

    private static string GetStatuses(string statuses)
    {
        return string.IsNullOrWhiteSpace(statuses) ? $"{Draft},{Published}" : statuses;
    }

    private sealed class ReferenceCountResult
    {
        public string NormalizedAssetUrl { get; init; }
        public int ReferencesCount { get; init; }
    }

    private sealed class ReferencePageResult
    {
        public string NormalizedAssetUrl { get; init; }
        public string Id { get; init; }
        public string Name { get; init; }
        public string Permalink { get; init; }
        public string CultureName { get; init; }
        public PageBuilderPageStatus Status { get; init; }
    }
}
