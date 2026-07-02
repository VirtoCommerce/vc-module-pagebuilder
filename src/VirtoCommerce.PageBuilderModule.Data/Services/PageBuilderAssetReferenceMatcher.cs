using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public static class PageBuilderAssetReferenceMatcher
{
    private static readonly string[] _referenceMarkers = ["/assets/", "/stores/", "https://", "http://"];
    private static readonly string[] _assetFileExtensions =
    [
        ".avif", ".bmp", ".csv", ".doc", ".docx", ".gif", ".ico", ".jpeg", ".jpg", ".pdf", ".png", ".ppt", ".pptx", ".svg", ".txt", ".webp", ".xls", ".xlsx", ".zip"
    ];
    private static readonly char[] _unquotedReferenceTerminators = ['"', '\'', '<', '>', ')', ',', ';', '?', '#', '&', '{', '}'];

    public static IReadOnlyDictionary<string, string> NormalizeAssetUrls(IEnumerable<string> assetUrls)
    {
        return assetUrls
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new { Original = x, Normalized = NormalizeAssetUrl(x) })
            .Where(x => !string.IsNullOrEmpty(x.Normalized))
            .GroupBy(x => x.Normalized, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Original, StringComparer.OrdinalIgnoreCase);
    }

    public static ISet<string> ExtractReferences(string content)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(content))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            VisitJsonElement(document.RootElement, result);
        }
        catch (JsonException)
        {
            ExtractReferencesFromString(content, result);
        }

        return result;
    }

    public static string NormalizeAssetUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            normalized = uri.AbsolutePath;
        }
        else
        {
            var queryIndex = normalized.IndexOfAny(['?', '#']);
            if (queryIndex >= 0)
            {
                normalized = normalized[..queryIndex];
            }
        }

        normalized = normalized.Replace('\\', '/');
        normalized = NormalizeAssetStoragePath(normalized);

        normalized = EnsureLeadingSlash(SafeUnescapeDataString(normalized));

        return normalized;
    }

    public static string GetAssetUrlHash(string normalizedAssetUrl)
    {
        if (string.IsNullOrWhiteSpace(normalizedAssetUrl))
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedAssetUrl.ToUpperInvariant())));
    }

    public static string NormalizeAssetFolderUrl(string value)
    {
        var normalized = NormalizeAssetUrl(value);

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.TrimEnd('/');
    }

    private static void VisitJsonElement(JsonElement element, ISet<string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    VisitJsonElement(property.Value, result);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    VisitJsonElement(item, result);
                }
                break;
            case JsonValueKind.String:
                ExtractReferencesFromString(element.GetString(), result);
                break;
        }
    }

    private static void ExtractReferencesFromString(string value, ISet<string> result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        AddExtractedReference(value, result);

        var startIndex = 0;
        while (TryFindReferenceStart(value, startIndex, out var referenceStart))
        {
            var token = ReadReferenceToken(value, referenceStart);
            AddExtractedReference(token, result);
            startIndex = referenceStart + Math.Max(token?.Length ?? 0, 1);
        }
    }

    private static bool TryFindReferenceStart(string value, int startIndex, out int result)
    {
        result = -1;

        foreach (var marker in _referenceMarkers)
        {
            var index = value.IndexOf(marker, startIndex, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (result < 0 || index < result))
            {
                result = index;
            }
        }

        return result >= 0;
    }

    private static string ReadReferenceToken(string value, int startIndex)
    {
        var quote = GetOpeningQuote(value, startIndex);
        var endIndex = startIndex;

        while (endIndex < value.Length && !IsReferenceTerminator(value, startIndex, endIndex, quote))
        {
            endIndex++;
        }

        return value[startIndex..endIndex];
    }

    private static char? GetOpeningQuote(string value, int startIndex)
    {
        if (startIndex <= 0)
        {
            return null;
        }

        var previous = value[startIndex - 1];
        return previous is '"' or '\'' ? previous : null;
    }

    private static bool IsReferenceTerminator(string source, int startIndex, int currentIndex, char? quote)
    {
        var value = source[currentIndex];

        return quote.HasValue
            ? IsQuotedReferenceTerminator(value, quote.Value)
            : IsUnquotedReferenceTerminator(source, startIndex, currentIndex);
    }

    private static bool IsQuotedReferenceTerminator(char value, char quote)
    {
        return value == quote;
    }

    private static bool IsUnquotedReferenceTerminator(string source, int startIndex, int currentIndex)
    {
        var value = source[currentIndex];

        return IsReferencePunctuationTerminator(value) ||
            char.IsWhiteSpace(value) && HasKnownAssetFileExtension(source, startIndex, currentIndex);
    }

    private static bool IsReferencePunctuationTerminator(char value)
    {
        return _unquotedReferenceTerminators.Contains(value);
    }

    private static bool HasKnownAssetFileExtension(string value, int startIndex, int endIndex)
    {
        var token = value[startIndex..endIndex].TrimEnd('/', '.', ',', ';');
        return _assetFileExtensions.Any(extension => token.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddExtractedReference(string value, ISet<string> result)
    {
        foreach (var referenceToken in SplitReferenceToken(value))
        {
            var trimmedToken = TrimReferenceToken(referenceToken);
            if (string.IsNullOrWhiteSpace(trimmedToken) || !HasExplicitReferenceMarker(trimmedToken))
            {
                continue;
            }

            var normalizedValue = NormalizeAssetUrl(trimmedToken);

            if (IsAssetReference(normalizedValue))
            {
                result.Add(normalizedValue);
            }
        }
    }

    private static string[] SplitReferenceToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var parts = value.Split(',');
        return parts.Length > 1 && parts.Skip(1).Any(ContainsReferenceMarker)
            ? parts
            : [value];
    }

    private static bool ContainsReferenceMarker(string value)
    {
        return _referenceMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAssetReference(string normalizedValue)
    {
        return normalizedValue?.StartsWith("/stores/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string NormalizeAssetStoragePath(string value)
    {
        const string assetsStoresMarker = "/assets/stores/";

        var assetsStoresIndex = value.IndexOf(assetsStoresMarker, StringComparison.OrdinalIgnoreCase);
        if (assetsStoresIndex >= 0)
        {
            return value[(assetsStoresIndex + "/assets".Length)..];
        }

        return value;
    }

    private static bool HasExplicitReferenceMarker(string value)
    {
        return _referenceMarkers.Any(marker => value.TrimStart().StartsWith(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string TrimReferenceToken(string value)
    {
        var result = value?.Trim().TrimEnd('/', '.', ',', ';');
        if (string.IsNullOrEmpty(result))
        {
            return result;
        }

        var descriptorIndex = result.LastIndexOfAny([' ', '\t', '\r', '\n']);
        if (descriptorIndex >= 0 && IsSrcSetDescriptor(result[(descriptorIndex + 1)..]))
        {
            result = result[..descriptorIndex];
        }

        return result;
    }

    private static bool IsSrcSetDescriptor(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var unit = value[^1];
        if (unit != 'w' && unit != 'x')
        {
            return false;
        }

        var number = value[..^1];
        if (string.IsNullOrEmpty(number))
        {
            return false;
        }

        return unit == 'w'
            ? number.All(char.IsDigit)
            : number.Any(char.IsDigit) && number.Count(x => x == '.') <= 1 && number.Where(x => x != '.').All(char.IsDigit);
    }

    private static string EnsureLeadingSlash(string value)
    {
        return value.StartsWith('/') ? value : $"/{value}";
    }

    private static string SafeUnescapeDataString(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }
}
