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

    [Fact]
    public void Summarizes_description_to_head_before_use_when()
    {
        var source = JObject.Parse("""{"description":"Hero banner. Use when: top of page. Skip when: elsewhere."}""");
        var target = new JObject();
        PageBuilderController.CopySchemaMetadata(source, target, "sections");
        Assert.Equal("Hero banner.", (string)target["description"]);
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

public class DeriveCatalogSummaryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Returns_null_or_empty_unchanged(string input) =>
        Assert.Equal(input, PageBuilderController.DeriveCatalogSummary(input));

    [Fact]
    public void Cuts_at_use_when_marker()
    {
        var result = PageBuilderController.DeriveCatalogSummary(
            "Author profile card with avatar and bio. Use when: showing a post author. Skip when: ...");
        Assert.Equal("Author profile card with avatar and bio.", result);
    }

    [Fact]
    public void Is_case_insensitive_on_marker()
    {
        var result = PageBuilderController.DeriveCatalogSummary("What it is. USE WHEN: something.");
        Assert.Equal("What it is.", result);
    }

    [Fact]
    public void Keeps_full_text_when_no_use_when_and_under_cap()
    {
        const string input = "A compact contact form with name, email and message.";
        Assert.Equal(input, PageBuilderController.DeriveCatalogSummary(input));
    }

    [Fact]
    public void Keeps_full_text_when_description_starts_with_use_when()
    {
        // Marker at index 0 is not a head boundary — keep the text as-is.
        const string input = "Use when you need a hero.";
        Assert.Equal(input, PageBuilderController.DeriveCatalogSummary(input));
    }

    [Fact]
    public void Caps_long_head_at_last_sentence_boundary()
    {
        var sentence1 = new string('a', 120) + ".";
        var input = sentence1 + " " + new string('b', 120); // > 200, no "Use when"
        var result = PageBuilderController.DeriveCatalogSummary(input);
        Assert.Equal(sentence1, result);
        Assert.True(result.Length <= PageBuilderController.CatalogSummaryMaxLength);
    }

    [Fact]
    public void Hard_cuts_with_ellipsis_when_no_sentence_boundary()
    {
        var input = new string('a', 250); // no punctuation, exceeds cap
        var result = PageBuilderController.DeriveCatalogSummary(input);
        Assert.EndsWith("…", result);
        Assert.True(result.Length <= PageBuilderController.CatalogSummaryMaxLength + 1);
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

public class TryValidatePageContentEnvelopeTests
{
    private static bool Validate(string content, out string error) =>
        PageBuilderPageController.TryValidatePageContentEnvelope(content, out error);

    [Fact]
    public void Accepts_minimal_valid_envelope()
    {
        var ok = Validate("""{"settings":{},"content":[]}""", out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void Accepts_envelope_with_sections()
    {
        var json = """{"settings":{"type":"settings","id":""},"content":[{"type":"title","id":"t1"},{"type":"text","id":"x2"}]}""";
        var ok = Validate(json, out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void Tolerates_extra_top_level_keys()
    {
        var ok = Validate("""{"settings":{},"content":[],"meta":"whatever"}""", out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ broken")]
    [InlineData("")]
    public void Rejects_invalid_json(string content)
    {
        var ok = Validate(content, out var error);
        Assert.False(ok);
        Assert.Contains("not valid JSON", error);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"hello\"")]
    [InlineData("null")]
    public void Rejects_non_object_root(string content)
    {
        var ok = Validate(content, out var error);
        Assert.False(ok);
        Assert.Contains("must be a JSON object", error);
    }

    [Fact]
    public void Rejects_missing_settings()
    {
        var ok = Validate("""{"content":[]}""", out var error);
        Assert.False(ok);
        Assert.Contains("`settings`", error);
    }

    [Theory]
    [InlineData("""{"settings":[],"content":[]}""")]
    [InlineData("""{"settings":"oops","content":[]}""")]
    [InlineData("""{"settings":null,"content":[]}""")]
    public void Rejects_non_object_settings(string content)
    {
        var ok = Validate(content, out var error);
        Assert.False(ok);
        Assert.Contains("`settings`", error);
    }

    [Fact]
    public void Rejects_missing_content()
    {
        var ok = Validate("""{"settings":{}}""", out var error);
        Assert.False(ok);
        Assert.Contains("`content`", error);
    }

    [Theory]
    [InlineData("""{"settings":{},"content":{}}""")]
    [InlineData("""{"settings":{},"content":"oops"}""")]
    [InlineData("""{"settings":{},"content":null}""")]
    public void Rejects_non_array_content(string content)
    {
        var ok = Validate(content, out var error);
        Assert.False(ok);
        Assert.Contains("`content`", error);
    }

    [Fact]
    public void Rejects_non_object_section_item()
    {
        var ok = Validate("""{"settings":{},"content":[{"type":"title","id":"t1"},"bad"]}""", out var error);
        Assert.False(ok);
        Assert.Contains("content[1]", error);
    }

    [Fact]
    public void Rejects_section_missing_type()
    {
        var ok = Validate("""{"settings":{},"content":[{"id":"t1"}]}""", out var error);
        Assert.False(ok);
        Assert.Contains("content[0].type", error);
    }

    [Theory]
    [InlineData("""{"settings":{},"content":[{"type":"","id":"t1"}]}""")]
    [InlineData("""{"settings":{},"content":[{"type":"   ","id":"t1"}]}""")]
    [InlineData("""{"settings":{},"content":[{"type":null,"id":"t1"}]}""")]
    public void Rejects_section_with_empty_type(string content)
    {
        var ok = Validate(content, out var error);
        Assert.False(ok);
        Assert.Contains("content[0].type", error);
    }

    [Fact]
    public void Rejects_section_with_non_string_type()
    {
        var ok = Validate("""{"settings":{},"content":[{"type":42,"id":"t1"}]}""", out var error);
        Assert.False(ok);
        Assert.Contains("content[0].type", error);
    }
}
