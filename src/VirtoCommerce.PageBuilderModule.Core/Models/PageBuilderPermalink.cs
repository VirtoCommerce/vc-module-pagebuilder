using System.Buffers;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderPermalink
{
    public static readonly string InvalidMessage =
        "Permalink cannot contain whitespace, control characters, the characters '?', '#', '&' or '\\', " +
        "empty segments ('//') or '.' and '..' segments.";

    // U+FEFF is not whitespace for char.IsWhiteSpace, but it is for the admin UI's /\s/: reject it here too,
    // so an API client cannot save a permalink the UI then reports as invalid.
    private static readonly SearchValues<char> ReservedCharacters = SearchValues.Create("?#&\\\uFEFF");

    // The permalink is opened as written: '?' and '#' would start the query and the fragment, '&' separates
    // query parameters and whitespace must be encoded. Letters of any language and '%' are allowed;
    // percent-escape sequences are not checked.
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
