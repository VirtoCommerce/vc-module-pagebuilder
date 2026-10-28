using System.Text.RegularExpressions;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// Name and permalink of a copy: "About us (copy)" and "/about-us-copy", then "(copy 2)" and "/about-us-copy-2" when
/// those permalinks are taken. A copy of a copy counts on from the original instead of stacking suffixes.
/// </summary>
public static partial class PageBuilderPageCopy
{
    [GeneratedRegex(@"\s*\(copy(?:\s+\d+)?\)$")]
    private static partial Regex NameSuffix();

    [GeneratedRegex(@"-copy(?:-\d+)?$")]
    private static partial Regex PermalinkSuffix();

    public static string GetName(string name, int number)
    {
        var baseName = NameSuffix().Replace(name ?? string.Empty, string.Empty);
        return number == 1 ? $"{baseName} (copy)" : $"{baseName} (copy {number})";
    }

    public static string GetPermalink(string permalink, int number)
    {
        if (string.IsNullOrEmpty(permalink))
        {
            return permalink;
        }

        var basePermalink = PermalinkSuffix().Replace(permalink.TrimEnd('/'), string.Empty);
        return number == 1 ? $"{basePermalink}-copy" : $"{basePermalink}-copy-{number}";
    }

    /// <summary>
    /// The first copy number for which none of the permalinks is taken, so a second copy never serves the addresses
    /// of the first one. One number for all permalinks keeps the languages of a Multilanguage Page together.
    /// </summary>
    public static int FindFreeNumber(IEnumerable<string> permalinks, IEnumerable<string> takenPermalinks)
    {
        var taken = new HashSet<string>(takenPermalinks.Where(x => x != null), StringComparer.OrdinalIgnoreCase);
        var sources = permalinks.Where(x => !string.IsNullOrEmpty(x)).ToList();

        var number = 1;
        while (sources.Exists(x => taken.Contains(GetPermalink(x, number))))
        {
            number++;
        }

        return number;
    }
}
