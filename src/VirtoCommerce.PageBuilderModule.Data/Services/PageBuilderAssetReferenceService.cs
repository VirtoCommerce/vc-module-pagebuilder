using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetReferenceService(
    IGroupedPageSearchService groupedPageSearchService,
    IGroupedPageService groupedPageService,
    PageBuilderAssetReferenceMatcher referenceMatcher)
    : IPageBuilderAssetReferenceService
{
    private const int _pageSize = 100;

    public async Task<PageBuilderAssetReferencesSearchResult> SearchReferencesAsync(PageBuilderAssetReferencesSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var normalizedAssets = referenceMatcher.NormalizeAssetUrls(criteria.AssetUrls ?? []);
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

        var allowedStatuses = GetStatuses(criteria.Statuses)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await foreach (var group in SearchGroups(criteria, cancellationToken))
        {
            var matchedAssets = await GetGroupReferences(group, references.Keys, allowedStatuses, cancellationToken);

            foreach (var matchedAsset in matchedAssets)
            {
                var reference = references[matchedAsset.Key];
                reference.ReferencesCount++;

                if (criteria.IncludePages)
                {
                    reference.Pages.Add(new PageBuilderAssetReferencePage
                    {
                        Id = group.Id,
                        Name = group.Name,
                        Permalink = group.Permalink,
                        CultureName = group.CultureName,
                        Status = string.Join(", ", matchedAsset.Value),
                    });
                }
            }
        }

        return CreateResult(references.Values);
    }

    private async IAsyncEnumerable<GroupedPageBuilderPage> SearchGroups(PageBuilderAssetReferencesSearchCriteria criteria, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var skip = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageCriteria = AbstractTypeFactory<PageBuilderPageSearchCriteria>.TryCreateInstance();
            pageCriteria.StoreId = criteria.StoreId;
            pageCriteria.Statuses = GetStatuses(criteria.Statuses);
            pageCriteria.LanguageCode = criteria.LanguageCode;
            pageCriteria.ObjectIds = criteria.ObjectIds;
            pageCriteria.Skip = skip;
            pageCriteria.Take = _pageSize;

            var searchResult = await groupedPageSearchService.SearchAsync(pageCriteria);

            foreach (var group in searchResult.Results ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return group;
            }

            skip += searchResult.Results?.Count ?? 0;

            if (skip >= searchResult.TotalCount || searchResult.Results.IsNullOrEmpty())
            {
                break;
            }
        }
    }

    private async Task<IDictionary<string, ISet<string>>> GetGroupReferences(GroupedPageBuilderPage group, IEnumerable<string> normalizedAssets, ISet<string> allowedStatuses, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in group.Pages.Where(x => allowedStatuses.Contains(x.Status)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await groupedPageService.LoadContent(page.Id, cancellationToken);
            var matchedAssets = referenceMatcher.FindReferences(content, normalizedAssets);

            foreach (var matchedAsset in matchedAssets)
            {
                if (!result.TryGetValue(matchedAsset, out var statuses))
                {
                    statuses = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[matchedAsset] = statuses;
                }

                statuses.Add(page.Status);
            }
        }

        return result;
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

    private static string GetStatuses(string statuses)
    {
        return string.IsNullOrWhiteSpace(statuses) ? $"{Draft},{Published}" : statuses;
    }
}
