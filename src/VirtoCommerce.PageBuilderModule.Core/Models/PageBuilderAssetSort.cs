using System.Collections.ObjectModel;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderAssetSort
{
    public static IReadOnlyDictionary<string, Comparison<BlobEntry>> Columns { get; } =
        new ReadOnlyDictionary<string, Comparison<BlobEntry>>(new Dictionary<string, Comparison<BlobEntry>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = (x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name),
            ["Type"] = (x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Type, y.Type),
            ["Size"] = (x, y) => ((x as BlobInfo)?.Size ?? 0).CompareTo((y as BlobInfo)?.Size ?? 0),
            ["ModifiedDate"] = (x, y) => Nullable.Compare(x.ModifiedDate, y.ModifiedDate),
        });

    public static IOrderedEnumerable<BlobEntry> Order(IEnumerable<BlobEntry> entries, IList<SortInfo> sortInfos)
    {
        var sorted = entries.OrderByDescending(x => x.Type == "folder");
        foreach (var sort in sortInfos)
        {
            var comparer = Comparer<BlobEntry>.Create(Columns[sort.SortColumn]);
            sorted = sort.SortDirection == SortDirection.Descending
                ? sorted.ThenByDescending(x => x, comparer)
                : sorted.ThenBy(x => x, comparer);
        }
        return sorted.ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RelativeUrl ?? x.Url, StringComparer.Ordinal);
    }
}
