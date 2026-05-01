using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class IsValidSchemaKindTests
{
    [Theory]
    [InlineData("sections")]
    [InlineData("templates")]
    [InlineData("blocks")]
    [InlineData("objects")]
    [InlineData("shared")]
    public void Returns_true_for_known_kinds(string kind) =>
        Assert.True(PageBuilderController.IsValidSchemaKind(kind));

    [Theory]
    [InlineData("")]
    [InlineData("Sections")]
    [InlineData("section")]
    [InlineData("pageSettings")]
    [InlineData(null)]
    public void Returns_false_for_unknown_kinds(string kind) =>
        Assert.False(PageBuilderController.IsValidSchemaKind(kind));
}

public class IsStaticEntryTests
{
    [Fact]
    public void Returns_false_when_static_property_absent() =>
        Assert.False(PageBuilderController.IsStaticEntry(JObject.Parse("{}")));

    [Fact]
    public void Returns_false_when_static_is_null() =>
        Assert.False(PageBuilderController.IsStaticEntry(JObject.Parse("""{"static":null}""")));

    [Fact]
    public void Returns_false_when_static_is_false() =>
        Assert.False(PageBuilderController.IsStaticEntry(JObject.Parse("""{"static":false}""")));

    [Fact]
    public void Returns_true_when_static_is_true() =>
        Assert.True(PageBuilderController.IsStaticEntry(JObject.Parse("""{"static":true}""")));

    [Theory]
    [InlineData("top")]
    [InlineData("bottom")]
    public void Returns_true_for_top_or_bottom(string value) =>
        Assert.True(PageBuilderController.IsStaticEntry(JObject.Parse($$"""{"static":"{{value}}"}""")));

    [Theory]
    [InlineData("yes")]
    [InlineData("middle")]
    [InlineData("")]
    [InlineData("TOP")]
    public void Returns_false_for_other_string_values(string value) =>
        Assert.False(PageBuilderController.IsStaticEntry(JObject.Parse($$"""{"static":"{{value}}"}""")));

    [Fact]
    public void Returns_false_for_numeric_static_value() =>
        Assert.False(PageBuilderController.IsStaticEntry(JObject.Parse("""{"static":1}""")));

    [Fact]
    public void Returns_false_for_null_entry() =>
        Assert.False(PageBuilderController.IsStaticEntry(null));
}

public class CopySchemaMetadataTests
{
    [Fact]
    public void Copies_name()
    {
        var source = JObject.Parse("""{"name":"Hero"}""");
        var target = new JObject();
        PageBuilderController.CopySchemaMetadata(source, target, "sections");
        Assert.Equal("Hero", (string)target["name"]);
    }

    [Theory]
    [InlineData("sections")]
    [InlineData("templates")]
    [InlineData("blocks")]
    public void Copies_description_for_picking_kinds(string kind)
    {
        var source = JObject.Parse("""{"description":"Use when..."}""");
        var target = new JObject();
        PageBuilderController.CopySchemaMetadata(source, target, kind);
        Assert.Equal("Use when...", (string)target["description"]);
    }

    [Theory]
    [InlineData("objects")]
    [InlineData("shared")]
    public void Skips_description_for_non_picking_kinds(string kind)
    {
        var source = JObject.Parse("""{"description":"Use when..."}""");
        var target = new JObject();
        PageBuilderController.CopySchemaMetadata(source, target, kind);
        Assert.Null(target["description"]);
    }

    [Fact]
    public void Skips_designer_only_and_resolution_metadata()
    {
        var source = JObject.Parse("""
            {
              "name":"Hero","icon":"image","displayField":"title",
              "tab":"Content","sort":10,"group":"Hero","groupSort":1,
              "static":"top","includeShared":["title"]
            }
            """);
        var target = new JObject();
        PageBuilderController.CopySchemaMetadata(source, target, "sections");
        Assert.Equal(new[] { "name" }, target.Properties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Skips_null_valued_properties()
    {
        var source = JObject.Parse("""{"name":null,"description":null}""");
        var target = new JObject();
        PageBuilderController.CopySchemaMetadata(source, target, "sections");
        Assert.Empty(target.Properties());
    }

    [Fact]
    public void Preserves_existing_target_keys()
    {
        var source = JObject.Parse("""{"name":"Hero"}""");
        var target = new JObject { ["key"] = "hero" };
        PageBuilderController.CopySchemaMetadata(source, target, "sections");
        Assert.Equal("hero", (string)target["key"]);
        Assert.Equal("Hero", (string)target["name"]);
    }
}

public class MergeStaticSectionsIntoTemplateTests
{
    private static string Merge(string templateJson, IReadOnlyDictionary<string, string> sections) =>
        PageBuilderController.MergeStaticSectionsIntoTemplate(templateJson, sections);

    [Fact]
    public void Returns_input_unchanged_when_template_json_invalid()
    {
        const string input = "not json";
        Assert.Equal(input, Merge(input, new Dictionary<string, string>()));
    }

    [Fact]
    public void Template_without_settings_starts_empty_then_merges_static()
    {
        var sections = new Dictionary<string, string>
        {
            ["seo"] = """{"static":"top","settings":[{"id":"meta","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"name":"page"}""", sections));
        var settings = (JArray)result["settings"];
        Assert.Single(settings);
        Assert.Equal("meta", (string)settings[0]["id"]);
    }

    [Fact]
    public void Existing_template_settings_preserved_static_appended_after()
    {
        var sections = new Dictionary<string, string>
        {
            ["seo"] = """{"static":"top","settings":[{"id":"meta","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"settings":[{"id":"header","type":"string"}]}""", sections));
        var settings = (JArray)result["settings"];
        Assert.Equal(2, settings.Count);
        Assert.Equal("header", (string)settings[0]["id"]);
        Assert.Equal("meta", (string)settings[1]["id"]);
    }

    [Fact]
    public void Sections_filter_includes_only_listed_static_sections()
    {
        var sections = new Dictionary<string, string>
        {
            ["seo"] = """{"static":"top","settings":[{"id":"a","type":"string"}]}""",
            ["footer"] = """{"static":"bottom","settings":[{"id":"b","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"sections":["seo"],"settings":[]}""", sections));
        var settings = (JArray)result["settings"];
        Assert.Single(settings);
        Assert.Equal("a", (string)settings[0]["id"]);
    }

    [Fact]
    public void Empty_sections_filter_means_all_apply()
    {
        var sections = new Dictionary<string, string>
        {
            ["seo"] = """{"static":"top","settings":[{"id":"a","type":"string"}]}""",
            ["footer"] = """{"static":"bottom","settings":[{"id":"b","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"sections":[],"settings":[]}""", sections));
        Assert.Equal(2, ((JArray)result["settings"]).Count);
    }

    [Fact]
    public void Missing_sections_filter_means_all_apply()
    {
        var sections = new Dictionary<string, string>
        {
            ["seo"] = """{"static":"top","settings":[{"id":"a","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"settings":[]}""", sections));
        Assert.Single((JArray)result["settings"]);
    }

    [Fact]
    public void Underscore_prefixed_keys_are_skipped()
    {
        var sections = new Dictionary<string, string>
        {
            ["_internal"] = """{"static":"top","settings":[{"id":"a","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"settings":[]}""", sections));
        Assert.Empty((JArray)result["settings"]);
    }

    [Fact]
    public void Non_static_sections_in_dictionary_are_skipped()
    {
        var sections = new Dictionary<string, string>
        {
            ["regular"] = """{"settings":[{"id":"a","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"settings":[]}""", sections));
        Assert.Empty((JArray)result["settings"]);
    }

    [Fact]
    public void Static_section_with_no_settings_appends_nothing()
    {
        var sections = new Dictionary<string, string>
        {
            ["seo"] = """{"static":"top"}"""
        };
        var result = JObject.Parse(Merge("""{"settings":[{"id":"header","type":"string"}]}""", sections));
        Assert.Single((JArray)result["settings"]);
    }

    [Fact]
    public void Unparseable_section_json_is_skipped()
    {
        var sections = new Dictionary<string, string>
        {
            ["broken"] = "not json",
            ["seo"] = """{"static":"top","settings":[{"id":"a","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"settings":[]}""", sections));
        var settings = (JArray)result["settings"];
        Assert.Single(settings);
        Assert.Equal("a", (string)settings[0]["id"]);
    }

    [Fact]
    public void Field_id_collisions_are_concatenated_without_dedup()
    {
        var sections = new Dictionary<string, string>
        {
            ["seo"] = """{"static":"top","settings":[{"id":"header","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"settings":[{"id":"header","type":"string"}]}""", sections));
        Assert.Equal(2, ((JArray)result["settings"]).Count);
    }

    [Fact]
    public void Filter_excluding_all_static_yields_only_template_settings()
    {
        var sections = new Dictionary<string, string>
        {
            ["seo"] = """{"static":"top","settings":[{"id":"a","type":"string"}]}"""
        };
        var result = JObject.Parse(Merge("""{"sections":["unknown"],"settings":[{"id":"header","type":"string"}]}""", sections));
        var settings = (JArray)result["settings"];
        Assert.Single(settings);
        Assert.Equal("header", (string)settings[0]["id"]);
    }
}
