using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Tests.ThemeSources;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class ThemeValidatorTests
{
    private readonly ThemeValidator _validator = new();

    [Fact]
    public void Validate_TypeMissingFromCatalog_ReturnsErrors()
    {
        var catalog = JObject.Parse("""{ "sections": { "banner": {} }, "blocks": {}, "shared": {} }""");
        var source = Source(
            templates: new()
            {
                ["page"] = JObject.Parse("""{ "settings": {}, "content": [ { "id": "a", "type": "banner" }, { "id": "p", "type": "page-content" } ] }"""),
                ["product.big"] = JObject.Parse("""{ "settings": {}, "content": [ { "id": "b", "type": "carousel" }, { "type": "banner" } ] }"""),
            },
            header: JObject.Parse("""{ "content": [ { "id": "h", "type": "banner", "blocks": [ { "id": "x", "type": "nope" } ] } ] }"""));

        var errors = _validator.Validate(source, catalog);

        Assert.Equal(
            [
                "templates/product.big.json: 'b' has a type the storefront does not have, 'carousel'",
                "templates/product.big.json: an item without an id or a type",
                "sections/header-group.json: 'x' has a type the storefront does not have, 'nope'",
            ],
            errors);
        Assert.Single(_validator.Validate(source, null));
    }

    [Fact]
    public void Validate_ValuesOfClosedSets_ReturnsAnErrorForEachValueTheCatalogLacks()
    {
        var catalog = JObject.Parse("""
            {
              "sections": {
                "banner": {
                  "settings": [
                    { "id": "tone", "type": "color-token" },
                    { "id": "size", "type": "select", "options": [ { "value": "s" }, { "value": 2 } ] },
                    { "id": "sizes", "type": "select", "multiple": true, "options": [ { "value": "s" }, { "value": "m" } ] },
                    { "id": "level", "type": "select", "required": true, "options": [ { "value": "h2" } ] },
                    { "id": "remote", "type": "select", "request": { "url": "/x" } },
                    { "id": "items", "type": "list", "element": [ { "id": "size", "type": "select", "options": [ { "value": "s" } ] } ] }
                  ]
                }
              },
              "shared": { "_sections": { "settings": [ { "id": "background", "type": "color-token" } ] } },
              "paletteTokens": { "light": [ "color_primary_500" ], "dark": [ "color_dark_only" ] }
            }
            """);
        var header = JObject.Parse("""
            { "content": [
              { "id": "ok", "type": "banner", "tone": "color_dark_only", "size": 2, "sizes": [ "s", "m" ], "level": "h2", "remote": "anything", "items": [ { "size": "s" } ], "background": "" },
              { "id": "bad", "type": "banner", "tone": "#ff0000", "size": "2", "sizes": [ "s", "xl" ], "remote": "anything", "items": [ { "size": "s" }, { "size": { "a": 1 } } ], "background": [ "x" ] }
            ] }
            """);

        var errors = _validator.Validate(Source(header: header), catalog);

        Assert.Equal(
            [
                "sections/header-group.json: 'bad' has a value in 'tone' the storefront does not have, '#ff0000'",
                "sections/header-group.json: 'bad' has a value in 'size' the storefront does not have, '2'",
                "sections/header-group.json: 'bad' has a value in 'sizes' the storefront does not have, 'xl'",
                "sections/header-group.json: 'bad' has no value in 'level', which the storefront requires",
                "sections/header-group.json: 'bad' has a value in 'items[1].size' the storefront does not have, an object",
                "sections/header-group.json: 'bad' has a value in 'background' the storefront does not have, a list",
            ],
            errors);
        Assert.Empty(_validator.Validate(Source(header: header), null));
    }

    [Fact]
    public void Validate_CatalogWithoutPaletteTokens_DoesNotCheckColorTokens()
    {
        var catalog = JObject.Parse("""{ "sections": { "banner": { "settings": [ { "id": "tone", "type": "color-token", "required": true, "default": "color_primary_500" } ] } } }""");
        var header = JObject.Parse("""{ "content": [ { "id": "a", "type": "banner", "tone": "#ff0000" }, { "id": "b", "type": "banner" } ] }""");

        Assert.Empty(_validator.Validate(Source(header: header), catalog));
    }

    [Fact]
    public void Validate_NonTextIdOrType_ReturnsErrors()
    {
        var source = Source(header: JObject.Parse("""{ "content": [ { "id": {}, "type": "banner" }, { "id": "b", "type": ["banner"] } ] }"""));

        Assert.Equal(
            ["sections/header-group.json: an item without an id or a type", "sections/header-group.json: an item without an id or a type"],
            _validator.Validate(source, JObject.Parse("""{ "sections": { "banner": {} } }""")));
    }

    [Fact]
    public void Validate_BlocksNotAList_ReturnsError()
    {
        var source = Source(header: JObject.Parse("""{ "content": [ { "id": "h", "type": "banner", "blocks": { "id": "x", "type": "nope" } } ] }"""));

        Assert.Equal(["sections/header-group.json: 'h' has blocks that are not a list"], _validator.Validate(source, null));
    }

    [Fact]
    public void Validate_SharedComponentInThemeFile_ReturnsErrors()
    {
        // A snapshot is read as it is, so the placement would reach the storefront unresolved.
        var catalog = JObject.Parse("""{ "sections": { "product-info": {} } }""");

        Assert.Equal(
        [
            "templates/product.json: 'ref1' places a shared component, which only a page can hold",
            "sections/footer-group.json: 'ref1' places a shared component, which only a page can hold",
        ], _validator.Validate(WithSharedComponent(), catalog));
    }

    [Fact]
    public void Validate_BlockWithItsSchemaInSections_IsAccepted()
    {
        var header = JObject.Parse("""{ "content": [ { "id": "h", "type": "site-header", "blocks": [ { "id": "b", "type": "banner", "size": "s" } ] } ] }""");

        Assert.Empty(_validator.Validate(Source(header: header), CatalogWithBlockAsSection));
    }

    [Fact]
    public void Validate_BlockWithItsSchemaInSections_ChecksItsValues()
    {
        var header = JObject.Parse("""{ "content": [ { "id": "h", "type": "site-header", "blocks": [ { "id": "b", "type": "banner", "size": "xl" } ] } ] }""");

        Assert.Equal(
            ["sections/header-group.json: 'b' has a value in 'size' the storefront does not have, 'xl'"],
            _validator.Validate(Source(header: header), CatalogWithBlockAsSection));
    }

    [Fact]
    public void Validate_SharedComponentWithoutCatalog_ReturnsNoErrors()
    {
        // Live files may predate the check.
        Assert.Empty(_validator.Validate(WithSharedComponent(), null));
    }
}
