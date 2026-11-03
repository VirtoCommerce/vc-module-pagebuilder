using System.Buffers;
using System.Globalization;
using System.Text;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderPermalink
{
    // The length of the permalink column.
    public const int MaxLength = 2048;

    public static readonly string InvalidMessage =
        "Permalink must be at most " + MaxLength + " characters and cannot contain whitespace, control or invisible " +
        "formatting characters, the characters ? # & % \\ \" < > { } | ^ `, empty segments ('//') or '.' and '..' segments.";

    // '?', '#', '&' and '%' would start the query, the fragment, a query parameter or an escape sequence, so a
    // permalink could not be opened as written; the others are not allowed unencoded in a URL path (RFC 3986).
    private static readonly SearchValues<char> ReservedCharacters = SearchValues.Create("?#&%\\\"<>{}|^`");

    // Letters of any language are allowed as they are.
    public static bool IsValid(string permalink)
    {
        if (string.IsNullOrEmpty(permalink))
        {
            return true;
        }

        if (permalink.Length > MaxLength ||
            permalink.AsSpan().ContainsAny(ReservedCharacters) ||
            permalink.EnumerateRunes().Any(IsInvisible) ||
            permalink.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        return !permalink.Split('/').Any(x => x is "." or "..");
    }

    // Formatting characters, such as the zero-width space and the byte order mark, cannot be seen in the address bar.
    // Checked per code point: a formatting character outside the BMP (for example a tag character) is a surrogate
    // pair, and each of its halves alone has the Surrogate category.
    private static bool IsInvisible(Rune character)
    {
        return Rune.IsWhiteSpace(character) ||
            Rune.IsControl(character) ||
            Rune.GetUnicodeCategory(character) == UnicodeCategory.Format;
    }
}
