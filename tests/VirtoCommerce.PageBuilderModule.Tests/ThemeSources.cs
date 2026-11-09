using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal static class ThemeSources
{
    public static readonly JObject CatalogWithBlockAsSection = JObject.Parse("""
        {
          "sections": {
            "site-header": { "settings": [], "blocks": ["banner"] },
            "banner": { "settings": [ { "id": "size", "type": "select", "options": [ { "value": "s" } ] }, { "id": "menu", "type": "menu" } ] }
          },
          "blocks": {},
          "shared": {}
        }
        """);

    public static ThemeSource Source(
        Dictionary<string, JToken> templates = null,
        JToken header = null,
        JToken footer = null,
        JToken brand = null,
        JToken settingsData = null,
        JToken palette = null,
        JToken paletteDark = null,
        JToken rules = null) => new()
        {
            ThemeName = "default",
            Templates = templates ?? new Dictionary<string, JToken>(),
            Header = header,
            Footer = footer,
            Brand = brand,
            SettingsData = settingsData,
            Palette = palette,
            PaletteDark = paletteDark,
            Rules = rules,
        };

    public static ThemeSource WithSharedComponent()
    {
        var placement = JObject.Parse("""{ "id": "ref1", "type": "componentRef", "componentRef": "c1" }""");
        return Source(
            templates: new Dictionary<string, JToken>
            {
                ["product"] = new JObject { ["content"] = new JArray(new JObject { ["id"] = "s0", ["type"] = "product-info" }, placement) },
            },
            footer: new JObject { ["content"] = new JArray(placement.DeepClone()) });
    }
}
