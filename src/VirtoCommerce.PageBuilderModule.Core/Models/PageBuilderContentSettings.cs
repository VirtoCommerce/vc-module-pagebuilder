using System.Text.Json.Nodes;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderContentSettings
{
    /// <summary>
    /// Creates a document when absent, or synchronizes the group-owned settings while preserving other content.
    /// </summary>
    public static string Synchronize(string content, GroupedPageBuilderPage group)
    {
        var source = string.IsNullOrWhiteSpace(content) ? ModuleConstants.DefaultPageContent : content;
        if (JsonNode.Parse(source) is not JsonObject root)
        {
            return content;
        }
        var settings = root["settings"] as JsonObject;
        var settingsMatch = settings != null && HasStringValue(settings["name"], group.Name)
            && HasStringValue(settings["permalink"], group.Permalink) && HasStringValue(settings["cultureName"], group.CultureName);
        if (!string.IsNullOrWhiteSpace(content) && settingsMatch)
        {
            return content;
        }
        settings ??= new JsonObject();
        root["settings"] = settings;
        settings["name"] = group.Name;
        settings["permalink"] = group.Permalink;
        settings["cultureName"] = group.CultureName;
        return root.ToJsonString();
    }

    private static bool HasStringValue(JsonNode node, string expected)
    {
        return node == null ? expected == null : node is JsonValue value && value.TryGetValue<string>(out var text) && text == expected;
    }
}
