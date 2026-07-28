using System.Text.Json;
using System.Text.Json.Nodes;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public static class PageBuilderLinkedComponentReferenceMatcher
{
    public static bool HasReferences(string pageContent)
    {
        return ExtractReferences(pageContent).Count > 0;
    }

    public static ISet<string> ExtractReferences(string pageContent)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var topLevelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var placementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in ParsePageContent(pageContent).OfType<JsonObject>())
        {
            ProcessTopLevelItem(item, result, placementIds, topLevelIds);
        }

        return result;
    }

    public static void ValidateComponentContent(string componentContent)
    {
        if (string.IsNullOrWhiteSpace(componentContent))
        {
            throw new InvalidDataException("Linked Component content is required.");
        }

        JsonNode root;
        try
        {
            root = JsonNode.Parse(componentContent);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Linked Component content must be valid JSON.", ex);
        }

        if (root is not JsonObject rootObject ||
            rootObject["settings"] is not JsonObject ||
            rootObject["content"] is not JsonArray content)
        {
            throw new InvalidDataException(
                "Linked Component content must use the TemplateModel shape: { settings: {}, content: [] }.");
        }

        if (ContainsReferenceMarker(rootObject))
        {
            throw new InvalidDataException("Nested Linked Component references are not supported.");
        }

        ValidateSections(content, "content");
    }

    internal static JsonArray GetPageContentArray(JsonNode root)
    {
        if (root is JsonObject rootObject && rootObject["content"] is JsonArray content)
        {
            return content;
        }

        // Legacy page format is [settings, ...sections]. It cannot safely host the new marker contract,
        // but remains readable and savable without being rejected.
        return [];
    }

    internal static bool TryGetReference(
        JsonObject value,
        out string placementId,
        out string linkedComponentId)
    {
        var hasExactShape = value.Count == 3 &&
            value.ContainsKey("id") &&
            value.ContainsKey("type") &&
            value.ContainsKey(LinkedComponents.ReferenceProperty);
        placementId = GetString(value["id"]);
        linkedComponentId = GetString(value[LinkedComponents.ReferenceProperty]);
        var type = GetString(value["type"]);

        return hasExactShape &&
            string.Equals(type, LinkedComponents.ReferenceType, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(placementId) &&
            !string.IsNullOrWhiteSpace(linkedComponentId);
    }

    private static bool ContainsReferenceMarker(JsonNode node)
    {
        if (node is JsonObject objectNode)
        {
            var type = GetString(objectNode["type"]);
            if (objectNode.ContainsKey(LinkedComponents.ReferenceProperty) ||
                string.Equals(type, LinkedComponents.ReferenceType, StringComparison.Ordinal))
            {
                return true;
            }

            return objectNode.Any(x => x.Value != null && ContainsReferenceMarker(x.Value));
        }

        return node is JsonArray arrayNode && arrayNode.Any(x => x != null && ContainsReferenceMarker(x));
    }

    private static JsonArray ParsePageContent(string pageContent)
    {
        if (string.IsNullOrWhiteSpace(pageContent))
        {
            return [];
        }

        try
        {
            var root = JsonNode.Parse(pageContent);
            if (root == null)
            {
                return [];
            }

            return GetPageContentArray(root);
        }
        catch (JsonException)
        {
            // Preserve compatibility with legacy/non-standard page payloads that the module already accepts.
            return [];
        }
    }

    private static void ProcessTopLevelItem(
        JsonObject item,
        ISet<string> references,
        ISet<string> placementIds,
        ISet<string> topLevelIds)
    {
        if (TryGetReference(item, out var placementId, out var linkedComponentId))
        {
            AddReference(references, placementIds, topLevelIds, placementId, linkedComponentId);
            return;
        }

        RegisterOrdinaryItem(item, placementIds, topLevelIds);
        ValidateNoReferenceMarker(item);
    }

    private static void AddReference(
        ISet<string> references,
        ISet<string> placementIds,
        ISet<string> topLevelIds,
        string placementId,
        string linkedComponentId)
    {
        if (!placementIds.Add(placementId) || !topLevelIds.Add(placementId))
        {
            throw new InvalidDataException(
                $"Linked Component placement id '{placementId}' conflicts with another top-level content item id.");
        }

        references.Add(linkedComponentId);
    }

    private static void RegisterOrdinaryItem(
        JsonObject item,
        ISet<string> placementIds,
        ISet<string> topLevelIds)
    {
        var itemId = GetString(item["id"]);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        if (placementIds.Contains(itemId))
        {
            throw new InvalidDataException(
                $"Linked Component placement id '{itemId}' conflicts with another top-level content item id.");
        }

        // Ordinary legacy sections may duplicate one another, but a later marker with the same id is rejected.
        topLevelIds.Add(itemId);
    }

    private static void ValidateNoReferenceMarker(JsonObject item)
    {
        if (ContainsReferenceMarker(item))
        {
            throw new InvalidDataException(
                "Linked Component references must be complete top-level content items with id, type 'componentRef', and componentRef.");
        }
    }

    private static void ValidateSections(JsonArray sections, string path)
    {
        var siblingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < sections.Count; index++)
        {
            var itemPath = $"{path}[{index}]";
            if (sections[index] is not JsonObject section)
            {
                throw new InvalidDataException($"Linked Component '{itemPath}' must be an object.");
            }

            var id = GetString(section["id"]);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(GetString(section["type"])))
            {
                throw new InvalidDataException(
                    $"Linked Component '{itemPath}' must have non-empty string id and type properties.");
            }

            if (!siblingIds.Add(id))
            {
                throw new InvalidDataException(
                    $"Linked Component '{path}' contains duplicate sibling id '{id}'.");
            }

            if (section.ContainsKey("blocks"))
            {
                if (section["blocks"] is not JsonArray blocks)
                {
                    throw new InvalidDataException($"Linked Component '{itemPath}.blocks' must be an array.");
                }

                ValidateSections(blocks, $"{itemPath}.blocks");
            }
        }
    }

    private static string GetString(JsonNode node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var result)
            ? result
            : null;
    }
}
