using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemeSnapshotBuilder : ThemeContentReader, IThemeSnapshotBuilder
{
    private const int SnapshotFormat = 1;
    private const int TemplateRulesVersion = 2;
    private const string MenuFieldType = "menu";

    private const string ProductPageType = "product";
    private const string CategoryPageType = "category";

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
            if (ThemeStorage.IsTemplateKey(key) && GetPageDocument(document) is { } pageDocument)
            {
                RemoveSharedComponents(pageDocument);
                result[key] = pageDocument;
            }
        }

        return result;
    }

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

    private void CollectMenus(JArray items, string kind, JObject catalog, ISet<string> menus)
    {
        foreach (var item in items?.OfType<JObject>() ?? [])
        {
            var fields = GetFields(GetItemSchema(catalog, kind, GetString(item, "type")), kind, catalog);
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
}
