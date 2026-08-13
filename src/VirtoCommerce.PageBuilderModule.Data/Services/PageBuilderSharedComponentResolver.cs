using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderSharedComponentResolver(
    IPageBuilderSharedComponentContentService contentService,
    ILogger<PageBuilderSharedComponentResolver> logger)
    : IPageBuilderSharedComponentResolver
{
    public async Task<string> ResolveAsync(string content, CancellationToken cancellationToken = default)
    {
        var componentContents = await LoadReferencedComponentsAsync([content], cancellationToken);
        return Expand(content, componentContents);
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadReferencedComponentsAsync(
        IEnumerable<string> pageContents,
        CancellationToken cancellationToken = default)
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pageContent in pageContents ?? [])
        {
            try
            {
                references.UnionWith(PageBuilderSharedComponentReferenceMatcher.ExtractReferences(pageContent));
            }
            catch (InvalidDataException)
            {
                // A page with a malformed marker contributes no references. Expand() throws for that page
                // alone, so one bad document cannot deny the rest of the batch its component contents.
            }
        }

        return references.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await contentService.LoadContentsAsync(references, cancellationToken);
    }

    public string Expand(string content, IReadOnlyDictionary<string, string> componentContents)
    {
        var references = PageBuilderSharedComponentReferenceMatcher.ExtractReferences(content);
        if (references.Count == 0)
        {
            return content;
        }

        if (!TryParsePage(content, out var root))
        {
            return content;
        }

        var pageContent = PageBuilderSharedComponentReferenceMatcher.GetPageContentArray(root);
        root["content"] = ResolvePageContent(
            pageContent,
            componentContents ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return root.ToJsonString();
    }

    private JsonArray ResolvePageContent(
        JsonArray pageContent,
        IReadOnlyDictionary<string, string> componentContents)
    {
        var resolvedContent = new JsonArray();

        foreach (var item in pageContent)
        {
            AddResolvedItem(resolvedContent, item, componentContents);
        }

        return resolvedContent;
    }

    private void AddResolvedItem(
        JsonArray resolvedContent,
        JsonNode item,
        IReadOnlyDictionary<string, string> componentContents)
    {
        if (item is JsonObject itemObject &&
            PageBuilderSharedComponentReferenceMatcher.TryGetReference(
                itemObject,
                out var placementId,
                out var sharedComponentId))
        {
            AddSharedComponentSections(
                resolvedContent,
                componentContents,
                placementId,
                sharedComponentId);
            return;
        }

        resolvedContent.Add(item?.DeepClone());
    }

    private void AddSharedComponentSections(
        JsonArray resolvedContent,
        IReadOnlyDictionary<string, string> componentContents,
        string placementId,
        string sharedComponentId)
    {
        if (!componentContents.TryGetValue(sharedComponentId, out var componentContent) ||
            !TryGetComponentSections(componentContent, out var componentSections))
        {
            logger.LogWarning(
                "Shared Component '{SharedComponentId}' referenced by placement '{PlacementId}' has no valid content.",
                sharedComponentId,
                placementId);
            return;
        }

        AddComponentSections(resolvedContent, componentSections, placementId);
    }

    private static void AddComponentSections(
        JsonArray resolvedContent,
        JsonArray componentSections,
        string placementId)
    {
        var placementToken = CreatePlacementToken(placementId);

        for (var sectionIndex = 0; sectionIndex < componentSections.Count; sectionIndex++)
        {
            var clone = componentSections[sectionIndex]?.DeepClone();
            RemapSectionIds(clone as JsonObject, placementToken, $"section-{sectionIndex}");
            resolvedContent.Add(clone);
        }
    }

    private static bool TryParsePage(string content, out JsonObject root)
    {
        root = [];

        try
        {
            if (JsonNode.Parse(content) is not JsonObject parsedRoot)
            {
                return false;
            }

            root = parsedRoot;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetComponentSections(string content, out JsonArray sections)
    {
        sections = [];

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            var root = JsonNode.Parse(content) as JsonObject;
            if (root?["content"] is not JsonArray componentSections)
            {
                return false;
            }

            sections = componentSections;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void RemapSectionIds(JsonObject section, string placementToken, string path)
    {
        if (section == null)
        {
            return;
        }

        var remappedId = $"lc{placementToken}{KeepAsciiLettersAndDigits(path)}";
        section["id"] = remappedId;

        if (!string.IsNullOrWhiteSpace(GetString(section["__id"])))
        {
            section["__id"] = remappedId;
        }

        if (section["blocks"] is JsonArray blocks)
        {
            for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                RemapSectionIds(
                    blocks[blockIndex] as JsonObject,
                    placementToken,
                    $"{path}-block-{blockIndex}");
            }
        }
    }

    private static string GetString(JsonNode node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var result)
            ? result
            : null;
    }

    private static string KeepAsciiLettersAndDigits(string value)
    {
        return string.Concat(value.Where(char.IsAsciiLetterOrDigit));
    }

    private static string CreatePlacementToken(string placementId)
    {
        return Convert.ToHexString(Encoding.UTF8.GetBytes(placementId)).ToLowerInvariant();
    }
}
