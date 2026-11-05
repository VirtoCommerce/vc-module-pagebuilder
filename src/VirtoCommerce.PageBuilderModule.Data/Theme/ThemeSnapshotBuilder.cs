using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>A theme's files as a publication reads them: the draft when there is one, else the live file.</summary>
public sealed class ThemeSource
{
    public string Theme { get; init; }

    /// <summary>Template documents by template key.</summary>
    public IReadOnlyDictionary<string, JToken> Templates { get; init; } = new Dictionary<string, JToken>();

    public JToken Rules { get; init; }
    public JToken Header { get; init; }
    public JToken Footer { get; init; }
    public JToken Brand { get; init; }

    /// <summary>settings_data.json: where the brand comes from until the theme has a brand file.</summary>
    public JToken SettingsData { get; init; }

    public JToken Palette { get; init; }
    public JToken PaletteDark { get; init; }
}

/// <summary>One publication: the snapshot JSON of every language, and the menu names they hold.</summary>
public sealed record ThemeSnapshotSet(IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> Menus);

/// <summary>
/// Builds and checks the snapshots of a publication. Pure: storage, locking and the cache are
/// <see cref="ThemePublisher"/>'s.
/// </summary>
public static class ThemeSnapshotBuilder
{
    public const int Format = 1;
    public const int RulesVersion = 2;
    public const string MenuFieldType = "menu";

    public static readonly string[] PageTypes = ["product", "category", "page"];

    /// <summary>A section every page template holds; the designer adds it to the storefront's sections.</summary>
    private const string PageContentSection = "page-content";

    private static readonly Regex TemplateKey = new("^(product|category|page)(\\.[a-z0-9]+(?:-[a-z0-9]+)*)?$", RegexOptions.None, TimeSpan.FromSeconds(1));

    // A translation dictionary's keys: language names with a subtag, as stores name their languages (en-US).
    private static readonly Regex LanguageName = new("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})+$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly JObject DefaultRules = JObject.Parse("""
        {
          "product": [
            {
              "id": "default-b2c",
              "template": "product.b2c",
              "when": { "property": { "name": "VirtoFrontend_UI_Layout", "equals": "B2C" } }
            }
          ]
        }
        """);

    /// <summary>The template key of a file in the theme's templates folder, or <c>null</c> when it names none.</summary>
    public static string TemplateKeyOf(string fileName)
    {
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = fileName[..^".json".Length];
        return TemplateKey.IsMatch(key) ? key : null;
    }

    /// <summary>What keeps the theme from being published against the catalog; empty when nothing does.</summary>
    /// <param name="catalog">The storefront's schemas; <c>null</c> checks the shape only.</param>
    public static IReadOnlyList<string> Validate(ThemeSource source, JObject catalog)
    {
        var errors = new List<string>();

        foreach (var (key, document) in source.Templates)
        {
            var file = $"templates/{key}.json";
            if (TemplateKeyOf($"{key}.json") == null)
            {
                errors.Add($"{file}: not a template key");
                continue;
            }

            if (ToDocument(document) is not { } doc)
            {
                errors.Add($"{file}: not a page document");
                continue;
            }

            CheckItems((JArray)doc["content"], "sections", file, catalog, errors, allowPageContent: key.StartsWith("page", StringComparison.Ordinal));
        }

        CheckGroup(source.Header, "sections/header-group.json", catalog, errors);
        CheckGroup(source.Footer, "sections/footer-group.json", catalog, errors);

        if (source.Rules != null && source.Rules is not JObject)
        {
            errors.Add("config/template_rules.json: not an object");
        }

        return errors;
    }

    public static ThemeSnapshotSet Build(
        ThemeSource source,
        JObject catalog,
        string catalogHash,
        IReadOnlyList<string> languages,
        string defaultLanguage,
        int publication,
        DateTime publishedDate)
    {
        var templates = new JObject();
        foreach (var (key, document) in source.Templates.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (TemplateKeyOf($"{key}.json") != null && ToDocument(document) is { } doc)
            {
                templates[key] = WithoutSharedComponents(doc);
            }
        }

        var header = WithoutSharedComponents(ToGroup(source.Header));
        var footer = WithoutSharedComponents(ToGroup(source.Footer));
        var menus = MenusOf(catalog, header, footer);

        var snapshot = new JObject
        {
            ["format"] = Format,
            ["publication"] = publication,
            ["publishedDate"] = publishedDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["theme"] = source.Theme,
            ["cultureName"] = null,
            ["catalogHash"] = catalogHash,
            ["rules"] = ToRules(source.Rules),
            ["templates"] = templates,
        };
        if (header != null)
        {
            snapshot["header"] = header;
        }
        if (footer != null)
        {
            snapshot["footer"] = footer;
        }
        snapshot["brand"] = ToBrand(source.Brand, source.SettingsData);
        if (ToPalette(source.Palette, source.PaletteDark) is { } palette)
        {
            snapshot["palette"] = palette;
        }
        snapshot["menus"] = new JArray(menus);

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in languages.Append(defaultLanguage).Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var copy = (JObject)snapshot.DeepClone();
            copy["cultureName"] = language;
            foreach (var part in new[] { "templates", "header", "footer" })
            {
                if (copy[part] is { } token)
                {
                    copy[part] = Translate(token, language, defaultLanguage);
                }
            }
            files[language] = copy.ToString(Formatting.None);
        }

        return new ThemeSnapshotSet(files, menus);
    }

    /// <summary>
    /// The rules in the current format: an id on every rule (kept when it has one), the names of the conditions they
    /// use, and the default rules of a page type the file has no list for.
    /// </summary>
    public static JObject ToRules(JToken rules)
    {
        var source = rules as JObject ?? new JObject();
        var result = new JObject { ["version"] = RulesVersion };
        var conditions = new SortedSet<string>(StringComparer.Ordinal);
        var lists = new JObject();

        foreach (var pageType in PageTypes)
        {
            var list = source[pageType] as JArray ?? DefaultRules[pageType] as JArray;
            if (list == null)
            {
                continue;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var normalized = new JArray();
            for (var index = 0; index < list.Count; index++)
            {
                if (list[index] is not JObject rule)
                {
                    continue;
                }

                var copy = (JObject)rule.DeepClone();
                var id = TextOf(copy, "id");
                if (string.IsNullOrWhiteSpace(id) || ids.Contains(id))
                {
                    id = $"{pageType}-{index}";
                    for (var n = 2; ids.Contains(id); n++)
                    {
                        id = $"{pageType}-{index}-{n}";
                    }
                }
                ids.Add(id);
                copy["id"] = id;

                if (copy["when"] is JObject when)
                {
                    foreach (var condition in when.Properties())
                    {
                        conditions.Add(condition.Name);
                    }
                }
                normalized.Add(copy);
            }
            lists[pageType] = normalized;
        }

        result["conditions"] = new JArray(conditions);
        foreach (var list in lists.Properties())
        {
            result[list.Name] = list.Value;
        }
        return result;
    }

    /// <summary>The text of a translation dictionary in <paramref name="language"/>, else the default language, else its first.</summary>
    public static string Pick(JObject dictionary, string language, string defaultLanguage)
    {
        string Text(string name) =>
            dictionary.Properties().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))?.Value.Value<string>();

        return Text(language) ?? Text(defaultLanguage) ?? dictionary.Properties().First().Value.Value<string>();
    }

    /// <summary>An object whose keys are all language names and whose values are all texts.</summary>
    public static bool IsTranslation(JToken token) =>
        token is JObject dictionary
        && dictionary.HasValues
        && dictionary.Properties().All(x => LanguageName.IsMatch(x.Name) && x.Value.Type == JTokenType.String);

    private static JToken Translate(JToken token, string language, string defaultLanguage)
    {
        switch (token)
        {
            case JObject dictionary when IsTranslation(dictionary):
                return Pick(dictionary, language, defaultLanguage);
            case JObject obj:
                foreach (var property in obj.Properties().ToList())
                {
                    property.Value = Translate(property.Value, language, defaultLanguage);
                }
                return obj;
            case JArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    array[i] = Translate(array[i], language, defaultLanguage);
                }
                return array;
            default:
                return token;
        }
    }

    /// <summary>A page document, from the stored form: an object, or the legacy array of the settings and the sections.</summary>
    private static JObject ToDocument(JToken token)
    {
        var (settings, content) = token switch
        {
            JObject doc => (doc["settings"], doc["content"]),
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

    private static JObject ToGroup(JToken token) =>
        token is JObject { } group && group["content"] is JArray content ? new JObject { ["content"] = content.DeepClone() } : null;

    /// <summary>The document without shared component placements: the storefront reads a snapshot as it is, and only a page resolves them.</summary>
    private static JObject WithoutSharedComponents(JObject document)
    {
        if (document?["content"] is JArray content)
        {
            foreach (var placement in content.Where(x => x is JObject item && TextOf(item, "type") == ModuleConstants.SharedComponents.ReferenceType).ToList())
            {
                placement.Remove();
            }
        }
        return document;
    }

    private static JObject ToBrand(JToken brand, JToken settingsData)
    {
        var result = new JObject();
        if (brand is JObject own)
        {
            Copy(own, "logoUrl", result, "logoUrl");
            Copy(own, "secondaryLogoUrl", result, "secondaryLogoUrl");
            Copy(own, "faviconUrl", result, "faviconUrl");
            return result;
        }

        if (CurrentSettings(settingsData) is { } settings)
        {
            Copy(settings, "logo_image", result, "logoUrl");
            Copy(settings, "logo_inverted_image", result, "secondaryLogoUrl");
            Copy(settings, "favicon_image", result, "faviconUrl");
        }
        return result;
    }

    /// <summary>settings_data.json's current settings: the object itself, or the preset it names.</summary>
    private static JObject CurrentSettings(JToken settingsData)
    {
        if (settingsData is not JObject data)
        {
            return null;
        }

        return data["current"] switch
        {
            JObject current => current,
            JValue { Type: JTokenType.String } name => (data["presets"] as JObject)?[(string)name] switch
            {
                JObject { } preset when preset["settings"] is JObject nested => nested,
                JObject preset => preset,
                _ => null,
            },
            _ => null,
        };
    }

    private static void Copy(JObject from, string name, JObject to, string targetName)
    {
        if (from[name] is JValue { Type: JTokenType.String } value && !string.IsNullOrWhiteSpace(value.Value<string>()))
        {
            to[targetName] = value.Value<string>();
        }
    }

    private static JObject ToPalette(JToken light, JToken dark)
    {
        static JObject Tokens(JToken token) =>
            token is JObject tokens && tokens.HasValues
                ? new JObject(tokens.Properties().Where(x => x.Value.Type == JTokenType.String).Select(x => new JProperty(x.Name, x.Value)))
                : null;

        if (Tokens(light) is not { } lightTokens)
        {
            return null;
        }

        var palette = new JObject { ["light"] = lightTokens };
        if (Tokens(dark) is { } darkTokens)
        {
            palette["dark"] = darkTokens;
        }
        return palette;
    }

    private static void CheckGroup(JToken token, string file, JObject catalog, List<string> errors)
    {
        if (token == null)
        {
            return;
        }

        if (token is not JObject group || group["content"] is not JArray content)
        {
            errors.Add($"{file}: not a section group");
            return;
        }

        CheckItems(content, "sections", file, catalog, errors, allowPageContent: false);
    }

    private static void CheckItems(JArray items, string kind, string file, JObject catalog, List<string> errors, bool allowPageContent)
    {
        foreach (var token in items)
        {
            if (token is not JObject item
                || TextOf(item, "id") is not { } id || string.IsNullOrWhiteSpace(id)
                || TextOf(item, "type") is not { } type || string.IsNullOrWhiteSpace(type))
            {
                errors.Add($"{file}: an item without an id or a type");
                continue;
            }

            if (type == ModuleConstants.SharedComponents.ReferenceType)
            {
                // a first publication, from live files that may predate this check, leaves it out instead (Build)
                if (catalog != null)
                {
                    errors.Add($"{file}: '{id}' places a shared component, which only a page can hold");
                }
                continue;
            }

            var known = catalog == null
                || SchemaOf(catalog, kind, type) != null
                || (allowPageContent && kind == "sections" && type == PageContentSection);
            if (!known)
            {
                errors.Add($"{file}: '{id}' has a type the storefront does not have, '{type}'");
            }

            switch (item["blocks"])
            {
                case null:
                    break;
                case JArray blocks:
                    CheckItems(blocks, "blocks", file, catalog, errors, allowPageContent: false);
                    break;
                default:
                    errors.Add($"{file}: '{id}' has blocks that are not a list");
                    break;
            }
        }
    }

    /// <summary>The link list names the header and footer hold in settings of the type <c>menu</c>.</summary>
    private static IReadOnlyList<string> MenusOf(JObject catalog, params JObject[] groups)
    {
        var menus = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (catalog == null)
        {
            return [];
        }

        foreach (var group in groups.Where(x => x != null))
        {
            CollectMenus(group["content"] as JArray, "sections", catalog, menus);
        }
        return menus.ToList();
    }

    private static void CollectMenus(JArray items, string kind, JObject catalog, ISet<string> menus)
    {
        foreach (var item in items?.OfType<JObject>() ?? [])
        {
            var fields = FieldsOf(SchemaOf(catalog, kind, TextOf(item, "type")), kind, catalog);
            CollectMenus(item, fields, menus);
            CollectMenus(item["blocks"] as JArray, "blocks", catalog, menus);
        }
    }

    private static void CollectMenus(JObject values, IReadOnlyList<JObject> fields, ISet<string> menus)
    {
        foreach (var field in fields)
        {
            var value = values[TextOf(field, "id") ?? string.Empty];
            if (TextOf(field, "type") == MenuFieldType && value is JValue { Type: JTokenType.String } name && !string.IsNullOrWhiteSpace(name.Value<string>()))
            {
                menus.Add(name.Value<string>());
            }
            else if (field["element"] is JArray element)
            {
                var elementFields = element.OfType<JObject>().ToList();
                foreach (var row in (value as JArray)?.OfType<JObject>() ?? (value is JObject single ? [single] : []))
                {
                    CollectMenus(row, elementFields, menus);
                }
            }
        }
    }

    /// <summary>A schema's fields with the shared ones it takes, as the designer reads them.</summary>
    private static IReadOnlyList<JObject> FieldsOf(JObject schema, string kind, JObject catalog)
    {
        if (schema == null)
        {
            return [];
        }

        var own = (schema["settings"] as JArray)?.OfType<JObject>().ToList() ?? [];
        var ownIds = own.Select(x => TextOf(x, "id")).Where(x => x != null).ToHashSet(StringComparer.Ordinal);
        IEnumerable<JObject> SharedFields(string key) => (SchemaOf(catalog, "shared", key)?["settings"] as JArray)?.OfType<JObject>() ?? [];

        var included = TextsOf(schema["includeShared"]).SelectMany(SharedFields);
        var shared = schema["excludeShared"] switch
        {
            JValue { Type: JTokenType.Boolean } exclude when exclude.Value<bool>() => included,
            JArray excluded => SharedFields(kind == "sections" ? "_sections" : "_blocks").Concat(included)
                .Where(x => !TextsOf(excluded).Contains(TextOf(x, "id"))),
            _ => SharedFields(kind == "sections" ? "_sections" : "_blocks").Concat(included),
        };

        return own.Concat(shared.Where(x => !ownIds.Contains(TextOf(x, "id") ?? string.Empty))).ToList();
    }

    // Theme files and catalogs are read as they are saved, whatever their shape: a value of another type than the one
    // expected is not there, rather than an error that would stop a publication.
    private static string TextOf(JObject item, string name) => item[name] is JValue { Type: JTokenType.String } value ? (string)value : null;

    private static IEnumerable<string> TextsOf(JToken list) =>
        (list as JArray)?.OfType<JValue>().Where(x => x.Type == JTokenType.String).Select(x => (string)x) ?? [];

    private static JObject SchemaOf(JObject catalog, string kind, string type) =>
        type == null ? null : (catalog?[kind] as JObject)?[type] as JObject;
}
