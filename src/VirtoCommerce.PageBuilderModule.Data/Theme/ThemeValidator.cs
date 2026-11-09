using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemeValidator : ThemeContentReader, IThemeValidator
{
    private const string ColorTokenFieldType = "color-token";
    private const string SelectFieldType = "select";
    private const string PaletteTokensKey = "paletteTokens";

    // The designer adds this section to every page template.
    private const string PageContentSection = "page-content";

    public virtual IList<string> Validate(ThemeSource source, JObject catalog)
    {
        var errors = source.InvalidFiles.Select(x => $"{x}: not valid JSON").ToList();

        foreach (var (key, document) in source.Templates)
        {
            var file = ThemeStorage.GetTemplateFile(key);
            if (!ThemeStorage.IsTemplateKey(key))
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
                // A first publication checks the shape only and removes shared components without failing.
                if (catalog != null)
                {
                    errors.Add($"{file}: '{id}' places a shared component, which only a page can hold");
                }
                continue;
            }

            var isKnown = catalog == null
                || GetItemSchema(catalog, kind, type) != null
                || (allowPageContent && kind == ModuleConstants.SchemaKinds.Sections && type == PageContentSection);
            if (!isKnown)
            {
                errors.Add($"{file}: '{id}' has a type the storefront does not have, '{type}'");
            }

            if (catalog != null)
            {
                ValidateValues(item, GetFields(GetItemSchema(catalog, kind, type), kind, catalog), $"{file}: '{id}'", catalog, errors);
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

    protected virtual IList<JToken> GetPaletteTokens(JObject catalog)
    {
        if (catalog?[PaletteTokensKey] is not JObject tokens)
        {
            return null;
        }

        return GetStrings(tokens["light"]).Concat(GetStrings(tokens["dark"])).Distinct(StringComparer.Ordinal).Select(x => (JToken)x).ToList();
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
}
