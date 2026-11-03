using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class ThemeSnapshotBuilderTests
    {
        private static readonly DateTime Published = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Rules_get_ids_and_the_conditions_they_use_and_a_page_type_without_rules_gets_the_default()
        {
            var rules = ThemeSnapshotBuilder.ToRules(JObject.Parse("""
                {
                  "version": 1,
                  "category": [
                    { "template": "category.sale", "when": { "inCategory": ["c-sale"] } },
                    { "id": "kept", "template": "category.new", "when": { "inCategory": ["c-new"], "property": { "name": "x", "equals": "y" } } }
                  ],
                  "page": []
                }
                """));

            Assert.Equal(2, rules.Value<int>("version"));
            Assert.Equal(["category-0", "kept"], rules["category"]!.Select(x => x.Value<string>("id")));
            Assert.Empty((JArray)rules["page"]!);
            // no product list: the default rule, which gives B2C products their template
            Assert.Equal("default-b2c", rules["product"]![0]!.Value<string>("id"));
            Assert.Equal(["inCategory", "property"], rules["conditions"]!.Values<string>());
        }

        [Fact]
        public void Two_rules_with_the_same_id_keep_the_first_one_and_the_other_gets_its_own()
        {
            var rules = ThemeSnapshotBuilder.ToRules(JObject.Parse("""
                { "product": [ { "id": "a", "template": "product" }, { "id": "a", "template": "product.b2c" } ] }
                """));

            Assert.Equal(["a", "product-1"], rules["product"]!.Select(x => x.Value<string>("id")));
        }

        [Fact]
        public void Each_language_gets_its_text_else_the_store_default_else_the_first_and_other_objects_stay()
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

            var set = ThemeSnapshotBuilder.Build(source, null, null, ["en-US", "de-DE"], "en-US", 1, Published);

            var de = Section(set.Files["de-DE"], "page");
            Assert.Equal("Kostenloser Versand", de.Value<string>("title"));
            Assert.Equal("Kaufen", de["button"]!.Value<string>("label"));
            Assert.Equal("/sale", de["button"]!.Value<string>("url"));
            Assert.Equal("Livraison", de.Value<string>("note"));
            Assert.Equal("red", de["filter"]!.Value<string>("equals"));
            Assert.Equal("Free shipping", Section(set.Files["en-US"], "page").Value<string>("title"));
        }

        [Fact]
        public void There_is_a_snapshot_for_every_store_language_and_for_the_default_one()
        {
            var set = ThemeSnapshotBuilder.Build(Source(), null, "abc12345", ["de-DE"], "en-US", 7, Published);

            Assert.Equal(["de-DE", "en-US"], set.Files.Keys.OrderBy(x => x));
            var snapshot = JObject.Parse(set.Files["de-DE"]);
            Assert.Equal(1, snapshot.Value<int>("format"));
            Assert.Equal(7, snapshot.Value<int>("publication"));
            Assert.Equal("de-DE", snapshot.Value<string>("cultureName"));
            Assert.Equal("abc12345", snapshot.Value<string>("catalogHash"));
            Assert.Equal("default", snapshot.Value<string>("theme"));
        }

        [Fact]
        public void A_legacy_array_template_reads_as_a_document()
        {
            var source = Source(templates: new() { ["product"] = JArray.Parse("""[ { "type": "settings" }, { "id": "s1", "type": "product-info" } ]""") });

            var snapshot = JObject.Parse(ThemeSnapshotBuilder.Build(source, null, null, [], "en-US", 1, Published).Files["en-US"]);

            Assert.Equal("product-info", snapshot["templates"]!["product"]!["content"]![0]!.Value<string>("type"));
            Assert.Equal("settings", snapshot["templates"]!["product"]!["settings"]!.Value<string>("type"));
        }

        [Fact]
        public void The_brand_is_the_brand_file_else_the_logos_of_the_theme_settings()
        {
            var fromFile = Snapshot(Source(brand: JObject.Parse("""{ "logoUrl": "/a.svg", "secondaryLogoUrl": "", "faviconUrl": "/f.ico" }""")));
            var fromSettings = Snapshot(Source(settingsData: JObject.Parse("""
                { "current": "Light", "presets": { "Light": { "settings": { "logo_image": "/l.svg", "logo_inverted_image": "/d.svg" } } } }
                """)));

            Assert.Equal(JObject.Parse("""{ "logoUrl": "/a.svg", "faviconUrl": "/f.ico" }"""), fromFile["brand"], JToken.EqualityComparer);
            Assert.Equal(JObject.Parse("""{ "logoUrl": "/l.svg", "secondaryLogoUrl": "/d.svg" }"""), fromSettings["brand"], JToken.EqualityComparer);
        }

        [Fact]
        public void A_palette_needs_its_light_part()
        {
            var both = Snapshot(Source(palette: JObject.Parse("""{ "color_primary_500": "#f99e24" }"""), paletteDark: JObject.Parse("""{ "color_primary_500": "#ffbb5b" }""")));
            var darkOnly = Snapshot(Source(paletteDark: JObject.Parse("""{ "color_primary_500": "#ffbb5b" }""")));

            Assert.Equal("#ffbb5b", both["palette"]!["dark"]!.Value<string>("color_primary_500"));
            Assert.Null(darkOnly["palette"]);
        }

        [Fact]
        public void The_menus_are_the_values_of_menu_settings_in_the_header_and_footer_blocks_and_lists_included()
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

            var set = ThemeSnapshotBuilder.Build(source, catalog, "abc12345", [], "en-US", 1, Published);

            Assert.Equal(["Help", "main-menu"], set.Menus);
            Assert.Equal(["Help", "main-menu"], JObject.Parse(set.Files["en-US"])["menus"]!.Values<string>());
        }

        [Fact]
        public void Against_a_catalog_a_section_the_storefront_does_not_have_stops_the_publication()
        {
            var catalog = JObject.Parse("""{ "sections": { "banner": {} }, "blocks": {}, "shared": {} }""");
            var source = Source(
                templates: new()
                {
                    ["page"] = JObject.Parse("""{ "settings": {}, "content": [ { "id": "a", "type": "banner" }, { "id": "p", "type": "page-content" } ] }"""),
                    ["product.big"] = JObject.Parse("""{ "settings": {}, "content": [ { "id": "b", "type": "carousel" }, { "type": "banner" } ] }"""),
                },
                header: JObject.Parse("""{ "content": [ { "id": "h", "type": "banner", "blocks": [ { "id": "x", "type": "nope" } ] } ] }"""));

            var errors = ThemeSnapshotBuilder.Validate(source, catalog);

            Assert.Equal(
                [
                    "templates/product.big.json: 'b' has a type the storefront does not have, 'carousel'",
                    "templates/product.big.json: an item without an id or a type",
                    "sections/header-group.json: 'x' has a type the storefront does not have, 'nope'",
                ],
                errors);
            // without a catalog only the shape counts
            Assert.Single(ThemeSnapshotBuilder.Validate(source, null));
        }

        [Theory]
        [InlineData("product.json", "product")]
        [InlineData("product.b2c.json", "product.b2c")]
        [InlineData("page.big-sale.json", "page.big-sale")]
        [InlineData("blog.json", null)]
        [InlineData("product.B2C.json", null)]
        [InlineData("product.json-draft", null)]
        public void A_template_file_name_gives_its_key(string fileName, string key)
        {
            Assert.Equal(key, ThemeSnapshotBuilder.TemplateKeyOf(fileName));
        }

        private static ThemeSource Source(
            Dictionary<string, JToken> templates = null,
            JToken header = null,
            JToken footer = null,
            JToken brand = null,
            JToken settingsData = null,
            JToken palette = null,
            JToken paletteDark = null) => new()
            {
                Theme = "default",
                Templates = templates ?? new Dictionary<string, JToken>(),
                Header = header,
                Footer = footer,
                Brand = brand,
                SettingsData = settingsData,
                Palette = palette,
                PaletteDark = paletteDark,
            };

        private static JObject Snapshot(ThemeSource source) =>
            JObject.Parse(ThemeSnapshotBuilder.Build(source, null, null, [], "en-US", 1, Published).Files["en-US"]);

        private static JObject Section(string snapshot, string template) =>
            (JObject)JObject.Parse(snapshot)["templates"]![template]!["content"]![0]!;
    }
}
