using System.Text;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Extensions;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetSearchService(IBlobStorageProvider blobProvider) : IPageBuilderAssetSearchService
{
    // Match the Designer's image filename fallback when a provider reports a generic MIME type.
    private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".apng", ".avif", ".bmp", ".gif", ".ico", ".jpg", ".jpeg", ".png", ".svg", ".webp",
    };

    public Task<BlobEntrySearchResult> SearchAsync(PageBuilderAssetSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        cancellationToken.ThrowIfCancellationRequested();
        return SearchInternalAsync(criteria, cancellationToken);
    }

    private async Task<BlobEntrySearchResult> SearchInternalAsync(PageBuilderAssetSearchCriteria criteria, CancellationToken cancellationToken)
    {
        // The Assets provider contract lists a location without paging. Passing no keyword avoids
        // its recursive search. Reuse that listing and page metadata here; never open file streams.
        var listing = await blobProvider.SearchAsync(criteria.FolderUrl, null);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = FilterEntries(listing.Results, criteria).ToArray();

        // Project the size so folders and blobs can share the same stable ordering.
        var sorted = matches.Select(x => new
        {
            Entry = x,
            x.Name,
            x.Type,
            Size = (x as BlobInfo)?.Size ?? 0,
            x.ModifiedDate,
            Url = x.RelativeUrl ?? x.Url,
        }).AsQueryable().OrderBySortInfos(criteria.SortInfos.Count > 0
            ? criteria.SortInfos
            : [new SortInfo { SortColumn = "Name", SortDirection = SortDirection.Ascending }]);

        return new BlobEntrySearchResult
        {
            TotalCount = matches.Length,
            Results = sorted.ThenBy(x => x.Name).ThenBy(x => x.Url)
                .Skip(criteria.Skip).Take(criteria.Take).Select(x => x.Entry).ToList(),
        };
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

    private static IEnumerable<BlobEntry> FilterAcceptedTypes(IEnumerable<BlobEntry> entries, string[] types)
    {
        var acceptedTypes = types?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()).ToArray() ?? [];
        if (acceptedTypes.Length > 0)
        {
            // Some providers omit MIME metadata. Use Platform's resolver so the picker can
            // apply the same selection checks to the returned entries as to uploaded files.
            foreach (var blob in entries.OfType<BlobInfo>().Where(x => string.IsNullOrWhiteSpace(x.ContentType)))
            {
                blob.ContentType = MimeTypeResolver.ResolveContentType(blob.Name ?? string.Empty);
            }
            entries = entries.Where(x => x.Type == "folder" || x is BlobInfo blob && acceptedTypes.Any(type => MatchesAcceptedType(blob, type)));
        }

        return entries;
    }

    private static bool MatchesAcceptedType(BlobInfo blob, string acceptedType)
    {
        if (acceptedType.StartsWith('.'))
        {
            return blob.Name?.EndsWith(acceptedType, StringComparison.OrdinalIgnoreCase) == true;
        }
        if (acceptedType.EndsWith("/*", StringComparison.Ordinal))
        {
            return blob.ContentType?.StartsWith(acceptedType[..^1], StringComparison.OrdinalIgnoreCase) == true
                || string.Equals(acceptedType, "image/*", StringComparison.OrdinalIgnoreCase) && _imageExtensions.Contains(Path.GetExtension(blob.Name));
        }
        return string.Equals(blob.ContentType, acceptedType, StringComparison.OrdinalIgnoreCase);
    }
}
