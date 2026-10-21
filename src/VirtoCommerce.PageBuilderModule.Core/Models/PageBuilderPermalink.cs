using System.Buffers;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderPermalink
{
    public const string InvalidMessage =
        "Permalink must be a URL path: it cannot contain whitespace, control characters, '?', '#', '&' or '\\', " +
        "empty segments ('//') or '.' and '..' segments.";

    private static readonly SearchValues<char> ReservedCharacters = SearchValues.Create("?#&\\");

    // The permalink is opened as written: '?' and '#' would start the query and the fragment, '&' separates
    // query parameters and whitespace must be encoded. Unicode letters and percent-encoded sequences are allowed.
    public static bool IsValid(string permalink)
    {
        if (string.IsNullOrEmpty(permalink))
        {
            return true;
        }

        if (permalink.AsSpan().ContainsAny(ReservedCharacters) ||
            permalink.Any(x => char.IsWhiteSpace(x) || char.IsControl(x)) ||
            permalink.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        return !permalink.Split('/').Any(x => x is "." or "..");
    }
}
