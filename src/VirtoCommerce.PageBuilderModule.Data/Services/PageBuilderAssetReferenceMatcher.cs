using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public static class PageBuilderAssetReferenceMatcher
{
    private static readonly string[] _referenceMarkers = ["/assets/", "/stores/", "https://", "http://"];

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

        if (normalized.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["/assets".Length..];
        }

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

        while (endIndex < value.Length && !IsReferenceTerminator(value[endIndex], quote))
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

    private static bool IsReferenceTerminator(char value, char? quote)
    {
        if (quote.HasValue)
        {
            return value == quote.Value || value == ',';
        }

        return char.IsWhiteSpace(value)
            || value is '"' or '\'' or '<' or '>' or ')' or ',' or ';' or '?' or '#' or '&' or '{' or '}';
    }

    private static void AddExtractedReference(string value, ISet<string> result)
    {
        var normalizedValue = NormalizeAssetUrl(TrimReferenceToken(value));

        if (IsAssetReference(normalizedValue))
        {
            result.Add(normalizedValue);
        }
    }

    private static bool IsAssetReference(string normalizedValue)
    {
        return normalizedValue?.StartsWith("/stores/", StringComparison.OrdinalIgnoreCase) == true;
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
