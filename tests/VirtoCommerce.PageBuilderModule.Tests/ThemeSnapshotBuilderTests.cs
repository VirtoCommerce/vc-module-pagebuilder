using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Tests.ThemeSources;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class ThemeSnapshotBuilderTests
{
    private static readonly DateTime Published = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private readonly ThemeSnapshotBuilder _builder = new();

    [Fact]
    public void Build_RulesWithoutIds_AssignsIdsConditionsAndTheDefaultRulesOfAPageTypeWithoutRules()
    {
        var rules = Snapshot(Source(rules: JObject.Parse("""
            {
              "version": 1,
              "category": [
                { "template": "category.sale", "when": { "inCategory": ["c-sale"] } },
                { "id": "kept", "template": "category.new", "when": { "inCategory": ["c-new"], "property": { "name": "x", "equals": "y" } } }
              ],
              "page": []
            }
            """)))["rules"]!;

        Assert.Equal(2, rules.Value<int>("version"));
        Assert.Equal(["category-0", "kept"], rules["category"]!.Select(x => x.Value<string>("id")));
        Assert.Empty((JArray)rules["page"]!);
        Assert.Equal("default-b2c", rules["product"]![0]!.Value<string>("id"));
        Assert.Equal(["inCategory", "property"], rules["conditions"]!.Values<string>());
    }

    [Fact]
    public void Build_TwoRulesWithTheSameId_KeepsTheFirstIdAndGivesTheOtherItsOwn()
    {
        var rules = Snapshot(Source(rules: JObject.Parse("""
            { "product": [ { "id": "a", "template": "product" }, { "id": "a", "template": "product.b2c" } ] }
            """)))["rules"]!;

        Assert.Equal(["a", "product-1"], rules["product"]!.Select(x => x.Value<string>("id")));
    }

    [Fact]
    public void Build_RulesTheStorefrontDoesNotRead_LeavesThemOutOfTheIdCount()
    {
        var rules = Snapshot(Source(rules: JObject.Parse("""
            {
              "product": [
                "rule",
                { "template": 5 },
                { "template": "product.a", "when": [ "inCategory" ] },
                { "template": "product.b", "when": null },
                { "template": "product.c" },
                { "id": " ", "template": "product.d", "when": { "productType": "Physical" } }
              ]
            }
            """)))["rules"]!;

        Assert.Equal(["product.c:product-0", "product.d: "], rules["product"]!.Select(x => $"{x.Value<string>("template")}:{x.Value<string>("id")}"));
        Assert.Equal(["productType"], rules["conditions"]!.Values<string>());
    }

    [Fact]
    public void Build_LocalizedTexts_FallsBackToDefaultThenFirstLanguage()
    {
        var source = Source(templates: new()
        {
            ["page"] = JObject.Parse("""
                {
                  "settings": { "type": "settings" },
                  "content": [
                    {
                      "id": "s1", "type": "banner",
                      "title": { "en-US": "Free shipping", "de-DE": "Kostenloser Versand" },
                      "note": { "fr-FR": "Livraison" },
                      "button": { "url": "/sale", "label": { "en-US": "Shop", "de-DE": "Kaufen" } },
                      "filter": { "name": "color", "equals": "red" }
                    },
                    { "id": "p", "type": "page-content" }
                  ]
                }
                """),
        });

        var set = _builder.Build(source, null, null, ["en-US", "de-DE"], "en-US", 1, Published);

        var de = Section(set.Files["de-DE"], "page");
        Assert.Equal("Kostenloser Versand", de.Value<string>("title"));
        Assert.Equal("Kaufen", de["button"]!.Value<string>("label"));
        Assert.Equal("/sale", de["button"]!.Value<string>("url"));
        Assert.Equal("Livraison", de.Value<string>("note"));
        Assert.Equal("red", de["filter"]!.Value<string>("equals"));
        Assert.Equal("Free shipping", Section(set.Files["en-US"], "page").Value<string>("title"));
    }

    [Fact]
    public void Build_LocalizedTexts_TakesBcp47NamesAndFallsBackToTheLanguagePart()
    {
        var source = Source(templates: new()
        {
            ["page"] = JObject.Parse("""
                {
                  "settings": { "type": "settings" },
                  "content": [
                    {
                      "id": "s1", "type": "banner",
                      "title": { "en-US": "Shipping", "SR-latn-rs": "Dostava" },
                      "part": { "en-US": "Shop", "de": "Kaufen", "de-DE": "Einkaufen" },
                      "same": { "en-US": "Cart", "de-DE": "Warenkorb" },
                      "skus": [ { "sku": "X1" }, { "en": "short", "de": "kurz" } ]
                    },
                    { "id": "p", "type": "page-content" }
                  ]
                }
                """),
        });

        var set = _builder.Build(source, null, null, ["en-US", "sr-Latn-RS", "de-AT"], "en-US", 1, Published);

        Assert.Equal("Dostava", Section(set.Files["sr-Latn-RS"], "page").Value<string>("title"));
        var at = Section(set.Files["de-AT"], "page");
        Assert.Equal("Kaufen", at.Value<string>("part"));
        Assert.Equal("Warenkorb", at.Value<string>("same"));
        Assert.Equal("X1", at["skus"]![0]!.Value<string>("sku"));
        Assert.Equal("kurz", at["skus"]![1]!.Value<string>("de"));
    }

    [Fact]
    public void Build_StoreLanguages_ReturnsSnapshotPerLanguageAndDefault()
    {
        var set = _builder.Build(Source(), null, "abc12345", ["de-DE"], "en-US", 7, Published);

        Assert.Equal(["de-DE", "en-US"], set.Files.Keys.OrderBy(x => x));
        var snapshot = JObject.Parse(set.Files["de-DE"]);
        Assert.Equal(1, snapshot.Value<int>("format"));
        Assert.Equal(7, snapshot.Value<int>("publication"));
        Assert.Equal("de-DE", snapshot.Value<string>("cultureName"));
        Assert.Equal("abc12345", snapshot.Value<string>("catalogHash"));
        Assert.Equal("default", snapshot.Value<string>("theme"));
    }

    [Fact]
    public void Build_LegacyArrayTemplate_ReadsItAsDocument()
    {
        var source = Source(templates: new() { ["product"] = JArray.Parse("""[ { "type": "settings" }, { "id": "s1", "type": "product-info" } ]""") });

        var snapshot = JObject.Parse(_builder.Build(source, null, null, [], "en-US", 1, Published).Files["en-US"]);

        Assert.Equal("product-info", snapshot["templates"]!["product"]!["content"]![0]!.Value<string>("type"));
        Assert.Equal("settings", snapshot["templates"]!["product"]!["settings"]!.Value<string>("type"));
    }

    [Fact]
    public void Build_Brand_UsesBrandFileElseThemeSettingsLogos()
    {
        var fromFile = Snapshot(Source(brand: JObject.Parse("""{ "logoUrl": "/a.svg", "secondaryLogoUrl": "", "faviconUrl": "/f.ico" }""")));
        var fromSettings = Snapshot(Source(settingsData: JObject.Parse("""
            { "current": "Light", "presets": { "Light": { "settings": { "logo_image": "/l.svg", "logo_inverted_image": "/d.svg" } } } }
            """)));

        Assert.Equal(JObject.Parse("""{ "logoUrl": "/a.svg", "faviconUrl": "/f.ico" }"""), fromFile["brand"], JToken.EqualityComparer);
        Assert.Equal(JObject.Parse("""{ "logoUrl": "/l.svg", "secondaryLogoUrl": "/d.svg" }"""), fromSettings["brand"], JToken.EqualityComparer);
    }

    [Fact]
    public void Build_PaletteWithoutLightPart_OmitsPalette()
    {
        var both = Snapshot(Source(palette: JObject.Parse("""{ "color_primary_500": "#f99e24" }"""), paletteDark: JObject.Parse("""{ "color_primary_500": "#ffbb5b" }""")));
        var darkOnly = Snapshot(Source(paletteDark: JObject.Parse("""{ "color_primary_500": "#ffbb5b" }""")));

        Assert.Equal("#ffbb5b", both["palette"]!["dark"]!.Value<string>("color_primary_500"));
        Assert.Null(darkOnly["palette"]);
    }

    [Fact]
    public void Build_MenuSettings_CollectsMenusFromHeaderAndFooter()
    {
        var catalog = JObject.Parse("""
            {
              "sections": {
                "site-header": { "settings": [ { "id": "catalog_menu", "type": "menu" }, { "id": "logo_width", "type": "number" } ] },
                "site-footer": { "settings": [], "blocks": ["footer-menu"] }
              },
              "blocks": {
                "footer-menu": { "settings": [ { "id": "columns", "type": "list", "element": [ { "id": "menu", "type": "menu" } ] } ] }
              },
              "shared": {}
            }
            """);
        var source = Source(
            header: JObject.Parse("""{ "content": [ { "id": "h", "type": "site-header", "catalog_menu": "main-menu", "logo_width": 120 } ] }"""),
            footer: JObject.Parse("""{ "content": [ { "id": "f", "type": "site-footer", "blocks": [ { "id": "b", "type": "footer-menu", "columns": [ { "menu": "Help" }, { "menu": "" } ] } ] } ] }"""));

        var set = _builder.Build(source, catalog, "abc12345", [], "en-US", 1, Published);

        Assert.Equal(["Help", "main-menu"], JObject.Parse(set.Files["en-US"])["menus"]!.Values<string>());
    }

    [Fact]
    public void Build_UnexpectedFileShapes_StillBuildsSnapshot()
    {
        var source = new ThemeSource
        {
            ThemeName = "default",
            Header = JObject.Parse("""{ "content": [ { "id": "h", "type": "banner", "menu": "main" } ] }"""),
            SettingsData = JObject.Parse("""{ "current": "dark", "presets": ["dark"] }"""),
            Rules = JObject.Parse("""{ "product": [ { "id": { "x": 1 }, "template": "product" } ] }"""),
        };
        var catalog = JObject.Parse("""
            {
                "sections": { "banner": { "settings": [ { "id": "menu", "type": "menu" }, { "id": [], "type": {} } ], "includeShared": [ {} ] } },
                "shared": []
            }
            """);

        var snapshot = JObject.Parse(_builder.Build(source, catalog, "abcdefgh", [], "en-US", 1, Published).Files["en-US"]);

        Assert.Equal(["main"], snapshot["menus"]!.Values<string>());
        Assert.Equal("product-0", snapshot["rules"]!["product"]![0]!.Value<string>("id"));
        Assert.Empty((JObject)snapshot["brand"]!);
    }

    [Fact]
    public void Build_SharedComponentWithoutCatalog_LeavesItOut()
    {
        // Live files may predate the check.
        var source = WithSharedComponent();

        var snapshot = JObject.Parse(_builder.Build(source, null, null, ["en-US"], "en-US", 1, DateTime.UtcNow).Files["en-US"]);
        Assert.Equal(["product-info"], snapshot["templates"]!["product"]!["content"]!.Select(x => x.Value<string>("type")));
        Assert.Empty((JArray)snapshot["footer"]!["content"]!);
    }

    [Fact]
    public void Build_FullTheme_WritesTheSnapshotJson()
    {
        var source = Source(
            templates: new()
            {
                ["product"] = JObject.Parse("""{ "settings": { "type": "settings" }, "content": [ { "id": "s1", "type": "product-info", "title": { "en-US": "Hi", "de-DE": "Hallo" } } ] }"""),
                ["page.landing"] = JArray.Parse("""[ { "type": "settings", "layout": "full" }, { "id": "p", "type": "page-content" } ]"""),
            },
            header: JObject.Parse("""{ "content": [ { "id": "h", "type": "site-header", "catalog_menu": "main-menu" } ], "order": ["h"] }"""),
            footer: JObject.Parse("""{ "content": [ { "id": "f", "type": "site-footer" } ] }"""),
            brand: JObject.Parse("""{ "logoUrl": "/a.svg", "secondaryLogoUrl": "", "faviconUrl": "/f.ico", "extra": "x" }"""),
            palette: JObject.Parse("""{ "color_primary_500": "#f99e24", "Radius": 4 }"""),
            paletteDark: JObject.Parse("""{ "color_primary_500": "#ffbb5b" }"""),
            rules: JObject.Parse("""{ "category": [ { "template": "category.sale", "when": { "inCategory": ["c-sale"] } } ] }"""));
        var catalog = JObject.Parse("""{ "sections": { "site-header": { "settings": [ { "id": "catalog_menu", "type": "menu" } ] } } }""");

        var set = _builder.Build(source, catalog, "abc12345", ["en-US", "de-DE"], "en-US", 3, Published);

        Assert.Equal(
            """{"format":1,"publication":3,"publishedDate":"2026-11-03T10:00:00.0000000Z","theme":"default","cultureName":"de-DE","catalogHash":"abc12345","rules":{"version":2,"conditions":["inCategory","property"],"product":[{"id":"default-b2c","template":"product.b2c","when":{"property":{"name":"VirtoFrontend_UI_Layout","equals":"B2C"}}}],"category":[{"template":"category.sale","when":{"inCategory":["c-sale"]},"id":"category-0"}]},"templates":{"page.landing":{"settings":{"type":"settings","layout":"full"},"content":[{"id":"p","type":"page-content"}]},"product":{"settings":{"type":"settings"},"content":[{"id":"s1","type":"product-info","title":"Hallo"}]}},"header":{"content":[{"id":"h","type":"site-header","catalog_menu":"main-menu"}]},"footer":{"content":[{"id":"f","type":"site-footer"}]},"brand":{"logoUrl":"/a.svg","faviconUrl":"/f.ico"},"palette":{"light":{"color_primary_500":"#f99e24"},"dark":{"color_primary_500":"#ffbb5b"}},"menus":["main-menu"]}""",
            set.Files["de-DE"]);
    }

    [Fact]
    public void Build_EmptyTheme_LeavesTheOptionalPartsOut()
    {
        var set = _builder.Build(Source(), null, null, [], "en-US", 1, Published);

        Assert.Equal(
            """{"format":1,"publication":1,"publishedDate":"2026-11-03T10:00:00.0000000Z","theme":"default","cultureName":"en-US","catalogHash":null,"rules":{"version":2,"conditions":["property"],"product":[{"id":"default-b2c","template":"product.b2c","when":{"property":{"name":"VirtoFrontend_UI_Layout","equals":"B2C"}}}]},"templates":{},"brand":{},"menus":[]}""",
            set.Files["en-US"]);
    }

    [Fact]
    public void Build_BlockWithItsSchemaInSections_CollectsItsMenus()
    {
        var header = JObject.Parse("""{ "content": [ { "id": "h", "type": "site-header", "blocks": [ { "id": "b", "type": "banner", "menu": "main-menu" } ] } ] }""");

        var set = _builder.Build(Source(header: header), CatalogWithBlockAsSection, "abc12345", [], "en-US", 1, Published);

        Assert.Equal(["main-menu"], JObject.Parse(set.Files["en-US"])["menus"]!.Values<string>());
    }

    private JObject Snapshot(ThemeSource source) =>
        JObject.Parse(_builder.Build(source, null, null, [], "en-US", 1, Published).Files["en-US"]);

    private static JObject Section(string snapshot, string template) =>
        (JObject)JObject.Parse(snapshot)["templates"]![template]!["content"]![0]!;
}
