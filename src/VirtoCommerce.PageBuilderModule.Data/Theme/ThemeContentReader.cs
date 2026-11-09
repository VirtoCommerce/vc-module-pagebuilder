using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>How a theme's documents and their values are read against the schema catalog, the same for validating them and for building the snapshot.</summary>
public abstract class ThemeContentReader
{
    protected const string ContentPageType = "page";

    private const string SharedSectionFields = "_sections";
    private const string SharedBlockFields = "_blocks";

    /// <summary>Also accepts the legacy array format.</summary>
    protected virtual JObject GetPageDocument(JToken token)
    {
        var (settings, content) = token switch
        {
            JObject document => (document["settings"], document["content"]),
            JArray legacy when legacy.Count > 0 => (legacy[0], new JArray(legacy.Skip(1))),
            _ => (null, null),
        };

        if (token is not (JObject or JArray) || (settings != null && settings is not JObject) || (content != null && content is not JArray))
        {
            return null;
        }

        return new JObject
        {
            ["settings"] = settings?.DeepClone() ?? new JObject { ["type"] = "settings" },
            ["content"] = content?.DeepClone() ?? new JArray(),
        };
    }

    protected virtual IList<JObject> GetFields(JObject schema, string kind, JObject catalog)
    {
        if (schema == null)
        {
            return [];
        }

        var ownFields = (schema["settings"] as JArray)?.OfType<JObject>().ToList() ?? [];
        var ownIds = ownFields.Select(x => GetString(x, "id")).Where(x => x != null).ToHashSet(StringComparer.Ordinal);
        var defaultSharedKey = kind == ModuleConstants.SchemaKinds.Sections ? SharedSectionFields : SharedBlockFields;

        IEnumerable<JObject> GetSharedFields(string key) =>
            (GetSchema(catalog, ModuleConstants.SchemaKinds.Shared, key)?["settings"] as JArray)?.OfType<JObject>() ?? [];

        var includedFields = GetStrings(schema["includeShared"]).SelectMany(GetSharedFields);
        var sharedFields = schema["excludeShared"] switch
        {
            JValue { Type: JTokenType.Boolean } exclude when exclude.Value<bool>() => includedFields,
            JArray excluded => GetSharedFields(defaultSharedKey).Concat(includedFields)
                .Where(x => !GetStrings(excluded).Contains(GetString(x, "id"))),
            _ => GetSharedFields(defaultSharedKey).Concat(includedFields),
        };

        return ownFields.Concat(sharedFields.Where(x => !ownIds.Contains(GetString(x, "id") ?? string.Empty))).ToList();
    }

    protected static void VisitValues(JObject values, IList<JObject> fields, string path, Action<JObject, JToken, string> visit)
    {
        foreach (var field in fields)
        {
            if (GetString(field, "id") is not { } id)
            {
                continue;
            }

            var value = values[id];
            var fieldPath = path == null ? id : $"{path}.{id}";
            visit(field, value, fieldPath);

            if (field["element"] is not JArray element)
            {
                continue;
            }

            var elementFields = element.OfType<JObject>().ToList();
            if (value is JObject single)
            {
                VisitValues(single, elementFields, fieldPath, visit);
            }

            var rows = value as JArray ?? [];
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i] is JObject row)
                {
                    VisitValues(row, elementFields, $"{fieldPath}[{i}]", visit);
                }
            }
        }
    }

    // A value of an unexpected type is treated as missing.
    protected static string GetString(JObject item, string name) => item[name] is JValue { Type: JTokenType.String } value ? (string)value : null;

    protected static IEnumerable<string> GetStrings(JToken list) =>
        (list as JArray)?.OfType<JValue>().Where(x => x.Type == JTokenType.String).Select(x => (string)x) ?? [];

    // a block's schema is looked up in blocks, then sections, as the designer does
    protected static JObject GetItemSchema(JObject catalog, string kind, string type) =>
        GetSchema(catalog, kind, type) ?? (kind == ModuleConstants.SchemaKinds.Blocks ? GetSchema(catalog, ModuleConstants.SchemaKinds.Sections, type) : null);

    private static JObject GetSchema(JObject catalog, string kind, string type) =>
        type == null ? null : (catalog?[kind] as JObject)?[type] as JObject;
}
