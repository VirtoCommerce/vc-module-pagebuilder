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
        var normalizedAssets = PageBuilderAssetReferenceMatcher.NormalizeAssetUrls(criteria.AssetUrls ?? []);
        var references = normalizedAssets.ToDictionary(
            x => x.Key,
            x => new PageBuilderAssetReference
            {
                AssetUrl = x.Value,
                NormalizedAssetUrl = x.Key,
            },
            StringComparer.OrdinalIgnoreCase);

        if (references.Count == 0 || string.IsNullOrWhiteSpace(criteria.StoreId))
        {
            return CreateResult(references.Values);
        }

        var normalizedAssetUrlHashes = references.Keys
            .Select(PageBuilderAssetReferenceMatcher.GetAssetUrlHash)
            .ToArray();
        var allowedStatuses = GetAllowedStatuses(criteria.Statuses)
            .ToArray();

        using var repository = repositoryFactory();
        var indexedReferences = repository.PageBuilderAssetReferences
            .Where(x => x.StoreId == criteria.StoreId)
            .Where(x => normalizedAssetUrlHashes.Contains(x.NormalizedAssetUrlHash))
            .Where(x => allowedStatuses.Contains(x.Status));

        if (!string.IsNullOrWhiteSpace(criteria.LanguageCode))
        {
            indexedReferences = indexedReferences.Where(x => x.CultureName == criteria.LanguageCode);
        }

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            indexedReferences = indexedReferences.Where(x => criteria.ObjectIds.Contains(x.GroupId));
        }

        var referenceGroups = await indexedReferences
            .GroupBy(x => x.NormalizedAssetUrl)
            .Select(x => new
            {
                NormalizedAssetUrl = x.Key,
                ReferencesCount = x.Select(r => r.GroupId).Distinct().Count(),
            })
            .ToListAsync(cancellationToken);

        foreach (var group in referenceGroups)
        {
            if (references.TryGetValue(group.NormalizedAssetUrl, out var reference))
            {
                reference.ReferencesCount = group.ReferencesCount;
            }
        }

        if (criteria.IncludePages)
        {
            var referencePages = await indexedReferences
                .Join(
                    repository.GroupedPageBuilderPages,
                    reference => reference.GroupId,
                    group => group.Id,
                    (reference, group) => new
                    {
                        reference.NormalizedAssetUrl,
                        group.Id,
                        group.Name,
                        group.Permalink,
                        group.CultureName,
                        reference.Status,
                    })
                .ToListAsync(cancellationToken);

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

        return CreateResult(references.Values);
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
}
