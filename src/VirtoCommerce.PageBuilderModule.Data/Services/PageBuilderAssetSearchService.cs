using System.Text;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetSearchService(IBlobStorageProvider blobProvider) : IPageBuilderAssetSearchService
{
    public Task<PageBuilderAssetSearchResult> SearchAsync(PageBuilderAssetSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        cancellationToken.ThrowIfCancellationRequested();
        return SearchInternalAsync(criteria, cancellationToken);
    }

    private async Task<PageBuilderAssetSearchResult> SearchInternalAsync(PageBuilderAssetSearchCriteria criteria, CancellationToken cancellationToken)
    {
        // The Assets provider contract lists a location without paging. Passing no keyword avoids
        // its recursive search. Reuse that listing and page metadata here; never open file streams.
        var listing = await blobProvider.SearchAsync(criteria.FolderUrl, null);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = FilterEntries(listing.Results, criteria).ToArray();

        var sorted = PageBuilderAssetSort.Order(matches, criteria.SortInfos).ToArray();
        var skip = Math.Min(criteria.Skip, Math.Max(0, (matches.Length - 1) / criteria.Take * criteria.Take));
        if (!string.IsNullOrEmpty(criteria.PreferredAssetUrl))
        {
            var index = Array.FindIndex(sorted, x => x.RelativeUrl == criteria.PreferredAssetUrl || x.Url == criteria.PreferredAssetUrl);
            if (index >= 0)
            {
                skip = index / criteria.Take * criteria.Take;
            }
        }

        var result = AbstractTypeFactory<PageBuilderAssetSearchResult>.TryCreateInstance();
        result.TotalCount = matches.Length;
        result.FileCount = matches.Count(x => x.Type == "blob");
        result.Skip = skip;
        result.Results = sorted.Skip(skip).Take(criteria.Take).Select(NormalizeEntry).ToList();
        return result;
    }

    private static IEnumerable<BlobEntry> FilterEntries(IEnumerable<BlobEntry> source, PageBuilderAssetSearchCriteria criteria)
    {
        var entries = FilterAcceptedTypes(source, criteria.AcceptedTypes);

        if (!string.IsNullOrEmpty(criteria.ExactName))
        {
            var exactName = criteria.ExactName.Trim().Normalize(NormalizationForm.FormC);
            return entries.Where(x => x.Type == "blob" && string.Equals(
                x.Name?.Trim().Normalize(NormalizationForm.FormC), exactName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim();
            return entries.Where(x => x.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true);
        }

        return entries;
    }

    private static IEnumerable<BlobEntry> FilterAcceptedTypes(IEnumerable<BlobEntry> entries, IList<string> types)
    {
        var acceptedTypes = types?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()).ToArray() ?? [];
        if (acceptedTypes.Length > 0)
        {
            entries = entries.Where(x => x.Type == "folder" || x is BlobInfo blob && acceptedTypes.Any(type => MatchesAcceptedType(blob, type)));
        }

        return entries;
    }

    private static bool MatchesAcceptedType(BlobInfo blob, string acceptedType)
    {
        var contentType = ResolveContentType(blob);
        if (acceptedType.StartsWith('.'))
        {
            return blob.Name?.EndsWith(acceptedType, StringComparison.OrdinalIgnoreCase) == true;
        }
        if (acceptedType.EndsWith("/*", StringComparison.Ordinal))
        {
            return contentType.StartsWith(acceptedType[..^1], StringComparison.OrdinalIgnoreCase) == true;
        }
        return string.Equals(contentType, acceptedType, StringComparison.OrdinalIgnoreCase);
    }

    private static BlobEntry NormalizeEntry(BlobEntry entry)
    {
        if (entry is not BlobInfo blob)
        {
            return entry;
        }
        var result = (BlobInfo)blob.Clone();
        result.ContentType = ResolveContentType(blob);
        return result;
    }

    private static string ResolveContentType(BlobInfo blob)
    {
        if (!string.IsNullOrWhiteSpace(blob.ContentType) && blob.ContentType != "application/octet-stream")
        {
            return blob.ContentType;
        }
        return Path.GetExtension(blob.Name)?.ToLowerInvariant() switch
        {
            ".apng" => "image/apng",
            ".avif" => "image/avif",
            _ => MimeTypeResolver.ResolveContentType(blob.Name ?? string.Empty),
        };
    }
}
