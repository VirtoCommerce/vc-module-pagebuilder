using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemeSnapshotBuilder : IThemeSnapshotBuilder
{
    private const int SnapshotFormat = 1;
    private const int TemplateRulesVersion = 2;
    private const string MenuFieldType = "menu";
    private const string ColorTokenFieldType = "color-token";
    private const string SelectFieldType = "select";
    private const string PaletteTokensKey = "paletteTokens";
    private const string JsonExtension = ".json";

    private const string ProductPageType = "product";
    private const string CategoryPageType = "category";
    private const string ContentPageType = "page";

    // The designer adds this section to every page template.
    private const string PageContentSection = "page-content";

    private const string SharedSectionFields = "_sections";
    private const string SharedBlockFields = "_blocks";

    private static readonly Regex TemplateKeyPattern = new("^(product|category|page)(\\.[a-z0-9]+(?:-[a-z0-9]+)*)?$", RegexOptions.None, TimeSpan.FromSeconds(1));

    // A culture name of BCP-47 shape: en, en-US, sr-Latn-RS.
    private static readonly Regex CultureNamePattern = new("^[a-z]{2,3}(-[a-z0-9]{2,8})*$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    // A dictionary has a culture name with a subtag, so that a row such as { "sku": "X1" } stays a row.
    private static readonly Regex SubtaggedCultureNamePattern = new("^[a-z]{2,3}(-[a-z0-9]{2,8})+$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static readonly JObject DefaultTemplateRules = JObject.Parse("""
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

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
        DateFormatString = "O",
    };

    public virtual string GetTemplateKey(string fileName)
    {
        if (!fileName.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = fileName[..^JsonExtension.Length];

        return IsTemplateKey(key) ? key : null;
    }

    public virtual IList<string> Validate(ThemeSource source, JObject catalog)
    {
        var errors = source.InvalidFiles.Select(x => $"{x}: not valid JSON").ToList();

        foreach (var (key, document) in source.Templates)
        {
            var file = $"{ThemeStorage.TemplatesFolder}/{key}{JsonExtension}";
            if (!IsTemplateKey(key))
            {
                errors.Add($"{file}: not a template key");
                continue;
            }

            if (GetPageDocument(document) is not { } pageDocument)
            {
                errors.Add($"{file}: not a page document");
                continue;
            }

            var allowPageContent = key.StartsWith(ContentPageType, StringComparison.Ordinal);
            ValidateItems((JArray)pageDocument["content"], ModuleConstants.SchemaKinds.Sections, file, catalog, errors, allowPageContent);
        }

        ValidateSectionGroup(source.Header, ThemeStorage.HeaderGroupFile, catalog, errors);
        ValidateSectionGroup(source.Footer, ThemeStorage.FooterGroupFile, catalog, errors);

        if (source.Rules != null && source.Rules is not JObject)
        {
            errors.Add($"{ThemeStorage.TemplateRulesFile}: not an object");
        }

        return errors;
    }

    public virtual ThemeSnapshotSet Build(
        ThemeSource source,
        JObject catalog,
        string catalogHash,
        IList<string> languages,
        string defaultLanguage,
        int publication,
        DateTime publishedDate)
    {
        var snapshot = CreateSnapshot(source, catalog, catalogHash, publication, publishedDate);

        var result = AbstractTypeFactory<ThemeSnapshotSet>.TryCreateInstance();
        result.Menus = snapshot.Menus;
        result.Files = GetSnapshotLanguages(languages, defaultLanguage).ToDictionary(
            x => x,
            x => JsonConvert.SerializeObject(TranslateSnapshot(snapshot, x, defaultLanguage), SerializerSettings),
            StringComparer.OrdinalIgnoreCase);

        return result;
    }

    protected virtual ThemeSnapshotContent CreateSnapshot(ThemeSource source, JObject catalog, string catalogHash, int publication, DateTime publishedDate)
    {
        var snapshot = AbstractTypeFactory<ThemeSnapshotContent>.TryCreateInstance();

        snapshot.Format = SnapshotFormat;
        snapshot.Publication = publication;
        snapshot.PublishedDate = publishedDate.ToUniversalTime();
        snapshot.Theme = source.ThemeName;
        snapshot.CatalogHash = catalogHash;
        snapshot.Rules = GetTemplateRules(source.Rules);
        snapshot.Templates = GetTemplates(source.Templates);
        snapshot.Header = GetSectionGroup(source.Header);
        snapshot.Footer = GetSectionGroup(source.Footer);
        snapshot.Brand = GetBrand(source.Brand, source.SettingsData);
        snapshot.Palette = GetPalette(source.Palette, source.PaletteDark);
        snapshot.Menus = GetMenus(catalog, snapshot.Header, snapshot.Footer);

        return snapshot;
    }

    protected virtual IList<string> GetSnapshotLanguages(IList<string> languages, string defaultLanguage)
    {
        return languages
            .Append(defaultLanguage)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    protected virtual ThemeSnapshotContent TranslateSnapshot(ThemeSnapshotContent snapshot, string language, string defaultLanguage)
    {
        var result = snapshot.CloneTyped();

        result.CultureName = language;
        result.Templates = result.Templates.ToDictionary(x => x.Key, x => (JObject)Translate(x.Value, language, defaultLanguage));
        result.Header = (JObject)Translate(result.Header, language, defaultLanguage);
        result.Footer = (JObject)Translate(result.Footer, language, defaultLanguage);

        return result;
    }

    protected virtual IDictionary<string, JObject> GetTemplates(IDictionary<string, JToken> templates)
    {
        var result = new Dictionary<string, JObject>();

        foreach (var (key, document) in templates.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (IsTemplateKey(key) && GetPageDocument(document) is { } pageDocument)
            {
                RemoveSharedComponents(pageDocument);
                result[key] = pageDocument;
            }
        }

        return result;
    }

    /// <summary>Normalizes the template rules: assigns missing rule ids, and takes the default rules of a page type the file does not list.</summary>
    protected virtual ThemeTemplateRules GetTemplateRules(JToken rules)
    {
        var source = rules as JObject ?? new JObject();
        var conditions = new SortedSet<string>(StringComparer.Ordinal);

        var result = AbstractTypeFactory<ThemeTemplateRules>.TryCreateInstance();
        result.Version = TemplateRulesVersion;
        result.Product = GetPageTypeRules(source, ProductPageType, conditions);
        result.Category = GetPageTypeRules(source, CategoryPageType, conditions);
        result.Page = GetPageTypeRules(source, ContentPageType, conditions);
        result.Conditions = conditions.ToList();

        return result;
    }

    protected virtual IList<JObject> GetPageTypeRules(JObject rules, string pageType, ISet<string> conditions)
    {
        if ((rules[pageType] as JArray ?? DefaultTemplateRules[pageType] as JArray) is not { } list)
        {
            return null;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<JObject>();

        foreach (var rule in list.OfType<JObject>().Where(IsTemplateRule))
        {
            var index = result.Count;
            var copy = (JObject)rule.DeepClone();
            var id = GetString(copy, "id");
            if (string.IsNullOrEmpty(id) || ids.Contains(id))
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
                conditions.UnionWith(when.Properties().Select(x => x.Name));
            }

            result.Add(copy);
        }

        return result;
    }

    /// <summary>Whether the storefront reads the rule: it has a template and its conditions, if any, are an object.</summary>
    protected virtual bool IsTemplateRule(JObject rule)
    {
        return rule["template"]?.Type == JTokenType.String && rule["when"] is null or JObject;
    }

    protected virtual JToken Translate(JToken token, string language, string defaultLanguage)
    {
        switch (token)
        {
            case JObject dictionary when IsTranslation(dictionary):
                return GetTranslation(dictionary, language, defaultLanguage);
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

    protected virtual bool IsTranslation(JObject dictionary)
    {
        return dictionary.Properties().Any(x => SubtaggedCultureNamePattern.IsMatch(x.Name))
            && dictionary.Properties().All(x => CultureNamePattern.IsMatch(x.Name) && x.Value.Type == JTokenType.String);
    }

    /// <summary>
    /// The text of the language, culture names compared ignoring case: the exact culture, else its language part
    /// (<c>de</c> for <c>de-AT</c>), else a culture of that language, else the store default language, else the first text.
    /// </summary>
    protected virtual string GetTranslation(JObject dictionary, string language, string defaultLanguage)
    {
        var languagePart = GetLanguagePart(language);

        string Find(Func<string, bool> matches) =>
            dictionary.Properties().FirstOrDefault(x => matches(x.Name))?.Value.Value<string>();

        return Find(x => string.Equals(x, language, StringComparison.OrdinalIgnoreCase))
            ?? Find(x => string.Equals(x, languagePart, StringComparison.OrdinalIgnoreCase))
            ?? Find(x => string.Equals(GetLanguagePart(x), languagePart, StringComparison.OrdinalIgnoreCase))
            ?? Find(x => string.Equals(x, defaultLanguage, StringComparison.OrdinalIgnoreCase))
            ?? dictionary.Properties().First().Value.Value<string>();
    }

    private static string GetLanguagePart(string cultureName)
    {
        return cultureName.Split('-')[0];
    }

    protected virtual bool IsTemplateKey(string key) => TemplateKeyPattern.IsMatch(key);

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

    protected virtual JObject GetSectionGroup(JToken token)
    {
        if (token is not JObject sectionGroup || sectionGroup["content"] is not JArray content)
        {
            return null;
        }

        var result = new JObject { ["content"] = content.DeepClone() };
        RemoveSharedComponents(result);

        return result;
    }

    // The storefront does not resolve shared components outside pages.
    protected virtual void RemoveSharedComponents(JObject document)
    {
        if (document["content"] is not JArray content)
        {
            return;
        }

        foreach (var placement in content.Where(x => x is JObject item && GetString(item, "type") == ModuleConstants.SharedComponents.ReferenceType).ToList())
        {
            placement.Remove();
        }
    }

    protected virtual ThemeBrand GetBrand(JToken brand, JToken settingsData)
    {
        var result = AbstractTypeFactory<ThemeBrand>.TryCreateInstance();

        if (brand is JObject brandFile)
        {
            result.LogoUrl = GetUrl(brandFile, "logoUrl");
            result.SecondaryLogoUrl = GetUrl(brandFile, "secondaryLogoUrl");
            result.FaviconUrl = GetUrl(brandFile, "faviconUrl");
        }
        else if (GetCurrentSettings(settingsData) is { } settings)
        {
            result.LogoUrl = GetUrl(settings, "logo_image");
            result.SecondaryLogoUrl = GetUrl(settings, "logo_inverted_image");
            result.FaviconUrl = GetUrl(settings, "favicon_image");
        }

        return result;
    }

    protected virtual JObject GetCurrentSettings(JToken settingsData)
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
                JObject preset when preset["settings"] is JObject nested => nested,
                JObject preset => preset,
                _ => null,
            },
            _ => null,
        };
    }

    protected virtual ThemePalette GetPalette(JToken light, JToken dark)
    {
        if (GetColorTokens(light) is not { } lightTokens)
        {
            return null;
        }

        var palette = AbstractTypeFactory<ThemePalette>.TryCreateInstance();
        palette.Light = lightTokens;
        palette.Dark = GetColorTokens(dark);

        return palette;
    }

    protected virtual void ValidateSectionGroup(JToken token, string file, JObject catalog, IList<string> errors)
    {
        if (token == null)
        {
            return;
        }

        if (token is not JObject sectionGroup || sectionGroup["content"] is not JArray content)
        {
            errors.Add($"{file}: not a section group");
            return;
        }

        ValidateItems(content, ModuleConstants.SchemaKinds.Sections, file, catalog, errors, allowPageContent: false);
    }

    protected virtual void ValidateItems(JArray items, string kind, string file, JObject catalog, IList<string> errors, bool allowPageContent)
    {
        foreach (var token in items)
        {
            if (token is not JObject item
                || GetString(item, "id") is not { } id || string.IsNullOrWhiteSpace(id)
                || GetString(item, "type") is not { } type || string.IsNullOrWhiteSpace(type))
            {
                errors.Add($"{file}: an item without an id or a type");
                continue;
            }

            if (type == ModuleConstants.SharedComponents.ReferenceType)
            {
                // A first publication removes shared components instead of failing.
                if (catalog != null)
                {
                    errors.Add($"{file}: '{id}' places a shared component, which only a page can hold");
                }
                continue;
            }

            var isKnown = catalog == null
                || GetSchema(catalog, kind, type) != null
                || (allowPageContent && kind == ModuleConstants.SchemaKinds.Sections && type == PageContentSection);
            if (!isKnown)
            {
                errors.Add($"{file}: '{id}' has a type the storefront does not have, '{type}'");
            }

            if (catalog != null)
            {
                ValidateValues(item, GetFields(GetSchema(catalog, kind, type), kind, catalog), $"{file}: '{id}'", catalog, errors);
            }

            switch (item["blocks"])
            {
                case null:
                    break;
                case JArray blocks:
                    ValidateItems(blocks, ModuleConstants.SchemaKinds.Blocks, file, catalog, errors, allowPageContent: false);
                    break;
                default:
                    errors.Add($"{file}: '{id}' has blocks that are not a list");
                    break;
            }
        }
    }

    protected virtual void ValidateValues(JObject item, IList<JObject> fields, string itemName, JObject catalog, IList<string> errors)
    {
        VisitValues(item, fields, path: null, (field, value, path) =>
        {
            if (GetValueError(field, value, path, catalog) is { } error)
            {
                errors.Add($"{itemName} {error}");
            }
        });
    }

    /// <summary>
    /// Why the storefront can't use the value of a field with a closed set of values: a <c>color-token</c> holds a palette
    /// token the catalog announces, a <c>select</c> one of its options (each of them, when <c>multiple</c>). Null when it can.
    /// </summary>
    protected virtual string GetValueError(JObject field, JToken value, string path, JObject catalog)
    {
        var type = GetString(field, "type");
        if (type is not (ColorTokenFieldType or SelectFieldType))
        {
            return null;
        }

        var chosen = value is JArray list && IsSet(field, "multiple") ? list.ToList() : [value];
        if (chosen.TrueForAll(IsEmptyValue))
        {
            // an absent value is the field's default
            var missing = IsSet(field, "required") && (value != null || IsEmptyValue(field["default"]));
            return missing ? $"has no value in '{path}', which the storefront requires" : null;
        }

        var allowed = type == ColorTokenFieldType ? GetPaletteTokens(catalog) : GetOptions(field);
        var bad = allowed == null ? null : chosen.Find(x => !IsEmptyValue(x) && !allowed.Any(option => JToken.DeepEquals(option, x)));

        return bad == null ? null : $"has a value in '{path}' the storefront does not have, {DescribeValue(bad)}";
    }

    /// <summary>The palette tokens the catalog announces, light and dark; null when it announces none.</summary>
    protected virtual IList<JToken> GetPaletteTokens(JObject catalog)
    {
        if (catalog?[PaletteTokensKey] is not JObject tokens)
        {
            return null;
        }

        return GetStrings(tokens["light"]).Concat(GetStrings(tokens["dark"])).Distinct(StringComparer.Ordinal).Select(x => (JToken)x).ToList();
    }

    protected virtual IList<string> GetMenus(JObject catalog, params JObject[] sectionGroups)
    {
        if (catalog == null)
        {
            return [];
        }

        var menus = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sectionGroup in sectionGroups.Where(x => x != null))
        {
            CollectMenus(sectionGroup["content"] as JArray, ModuleConstants.SchemaKinds.Sections, catalog, menus);
        }

        return menus.ToList();
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

    private void CollectMenus(JArray items, string kind, JObject catalog, ISet<string> menus)
    {
        foreach (var item in items?.OfType<JObject>() ?? [])
        {
            var fields = GetFields(GetSchema(catalog, kind, GetString(item, "type")), kind, catalog);
            CollectMenus(item, fields, menus);
            CollectMenus(item["blocks"] as JArray, ModuleConstants.SchemaKinds.Blocks, catalog, menus);
        }
    }

    private static void CollectMenus(JObject values, IList<JObject> fields, ISet<string> menus)
    {
        VisitValues(values, fields, path: null, (field, value, _) =>
        {
            if (GetString(field, "type") == MenuFieldType && value is JValue { Type: JTokenType.String } name && !string.IsNullOrWhiteSpace(name.Value<string>()))
            {
                menus.Add(name.Value<string>());
            }
        });
    }

    /// <summary>Visits each field's value, and the fields of a list's rows or of an object; the path names the field, a row by its index.</summary>
    private static void VisitValues(JObject values, IList<JObject> fields, string path, Action<JObject, JToken, string> visit)
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

    // a select with remote options has no list to check against
    private static List<JToken> GetOptions(JObject field) =>
        field["request"] != null ? null : (field["options"] as JArray)?.OfType<JObject>().Select(x => x["value"]).Where(x => x != null).ToList();

    private static bool IsSet(JObject field, string name) => field[name] is JValue { Type: JTokenType.Boolean } flag && (bool)flag;

    private static bool IsEmptyValue(JToken value) =>
        value == null || value.Type == JTokenType.Null || (value is JValue { Type: JTokenType.String } text && string.IsNullOrEmpty((string)text)) || value is JArray { Count: 0 };

    // a scalar as it is, a list or an object by its kind, so that no content beyond the value goes into the error
    private static string DescribeValue(JToken value) => value switch
    {
        JValue { Type: JTokenType.String } text => $"'{(string)text}'",
        JValue scalar => scalar.ToString(Formatting.None),
        JArray => "a list",
        _ => "an object",
    };

    private static Dictionary<string, string> GetColorTokens(JToken token)
    {
        return token is JObject tokens && tokens.HasValues
            ? tokens.Properties().Where(x => x.Value.Type == JTokenType.String).ToDictionary(x => x.Name, x => (string)x.Value)
            : null;
    }

    private static string GetUrl(JObject source, string name)
    {
        return source[name] is JValue { Type: JTokenType.String } value && !string.IsNullOrWhiteSpace(value.Value<string>()) ? value.Value<string>() : null;
    }

    // A value of an unexpected type is treated as missing.
    private static string GetString(JObject item, string name) => item[name] is JValue { Type: JTokenType.String } value ? (string)value : null;

    private static IEnumerable<string> GetStrings(JToken list) =>
        (list as JArray)?.OfType<JValue>().Where(x => x.Type == JTokenType.String).Select(x => (string)x) ?? [];

    private static JObject GetSchema(JObject catalog, string kind, string type) =>
        type == null ? null : (catalog?[kind] as JObject)?[type] as JObject;
}
