using System.Text.Json;
using System.Text.Json.Nodes;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public static class PageContentEnvelope
{
    public static bool TryValidate(string content, out string error)
    {
        JsonNode node;
        try
        {
            node = JsonNode.Parse(content);
        }
        catch (JsonException ex)
        {
            error = $"Content is not valid JSON: {ex.Message}";
            return false;
        }

        if (node is not JsonObject root)
        {
            error = "Content root must be a JSON object with `settings` and `content` keys.";
            return false;
        }

        if (root["settings"] is not JsonObject)
        {
            error = "`settings` must be a JSON object.";
            return false;
        }

        if (root["content"] is not JsonArray contentArray)
        {
            error = "`content` must be a JSON array of section objects.";
            return false;
        }

        for (var i = 0; i < contentArray.Count; i++)
        {
            if (contentArray[i] is not JsonObject section)
            {
                error = $"`content[{i}]` must be a JSON object.";
                return false;
            }

            string sectionType = null;
            if (section["type"] is JsonValue typeValue)
            {
                typeValue.TryGetValue<string>(out sectionType);
            }

            if (string.IsNullOrWhiteSpace(sectionType))
            {
                error = $"`content[{i}].type` is required (non-empty string matching a section key from the schema catalog).";
                return false;
            }
        }

        error = null;
        return true;
    }
}
