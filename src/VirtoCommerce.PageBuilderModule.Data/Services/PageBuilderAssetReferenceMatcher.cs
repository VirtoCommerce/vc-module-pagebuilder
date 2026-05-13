using System.Text.Json;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetReferenceMatcher
{
    public IReadOnlyDictionary<string, string> NormalizeAssetUrls(IEnumerable<string> assetUrls)
    {
        return assetUrls
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new { Original = x, Normalized = NormalizeAssetUrl(x) })
            .Where(x => !string.IsNullOrEmpty(x.Normalized))
            .GroupBy(x => x.Normalized, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Original, StringComparer.OrdinalIgnoreCase);
    }

    public ISet<string> FindReferences(string content, IEnumerable<string> normalizedAssetUrls)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(content))
        {
            return result;
        }

        var candidates = normalizedAssetUrls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x, BuildCandidates, StringComparer.OrdinalIgnoreCase);

        if (!candidates.Any())
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            VisitJsonElement(document.RootElement, candidates, result);
        }
        catch (JsonException)
        {
            FindReferencesInString(content, candidates, result);
        }

        return result;
    }

    public string NormalizeAssetUrl(string value)
    {
        return NormalizeAssetUrlCore(value);
    }

    private static string NormalizeAssetUrlCore(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri))
        {
            normalized = absoluteUri.AbsolutePath;
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

    private static void VisitJsonElement(JsonElement element, IReadOnlyDictionary<string, IReadOnlyList<string>> candidates, ISet<string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    VisitJsonElement(property.Value, candidates, result);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    VisitJsonElement(item, candidates, result);
                }
                break;
            case JsonValueKind.String:
                FindReferencesInString(element.GetString(), candidates, result);
                break;
        }
    }

    private static void FindReferencesInString(string value, IReadOnlyDictionary<string, IReadOnlyList<string>> candidates, ISet<string> result)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var normalizedValue = NormalizeAssetUrlCore(value);
        if (!string.IsNullOrEmpty(normalizedValue) && candidates.ContainsKey(normalizedValue))
        {
            result.Add(normalizedValue);
            return;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Value.Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(candidate.Key);
            }
        }
    }

    private static IReadOnlyList<string> BuildCandidates(string normalizedAssetUrl)
    {
        var decoded = EnsureLeadingSlash(SafeUnescapeDataString(normalizedAssetUrl));
        var encoded = EncodePath(decoded);

        return
        [
            decoded,
            encoded,
            $"/assets{decoded}",
            $"/assets{encoded}",
        ];
    }

    private static string EncodePath(string value)
    {
        return string.Join("/", value.Split('/').Select(Uri.EscapeDataString)).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
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
