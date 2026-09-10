using System;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// Serialization must depend on the page and nothing else — not on the host's line endings, not on
    /// how the environment happened to store the page last time. Otherwise publish status, which is a
    /// byte comparison between two branches, reports changes that are not there.
    /// </summary>
    public class PageJsonTests
    {
        private const char LineSeparator = '\u2028';
        private const char ParagraphSeparator = '\u2029';
        private const char Bom = '\uFEFF';

        [Fact]
        public void Serialize_uses_lf_and_two_space_indent()
        {
            var page = JObject.Parse("""{ "settings": { "type": "settings" }, "content": [] }""");

            var json = PageJson.Serialize(page);

            Assert.DoesNotContain("\r", json, StringComparison.Ordinal);
            Assert.Contains("\n  \"settings\": {", json, StringComparison.Ordinal);
            Assert.Contains("\n    \"type\": \"settings\"", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Serialize_is_stable_across_the_shapes_the_save_endpoint_produces()
        {
            var asToken = PageJson.Serialize(JObject.Parse("""{ "a": 1 }"""));
            var asPoco = PageJson.Serialize(new { a = 1 });

            Assert.Equal(asToken, asPoco);
        }

        [Fact]
        public void Serialize_of_null_is_a_json_null_not_an_empty_string()
        {
            Assert.Equal("null\n", PageJson.Serialize(null));
        }

        [Fact]
        public void Serialize_ends_with_a_newline()
        {
            Assert.EndsWith("}\n", PageJson.Serialize(JObject.Parse("""{ "a": 1 }""")), StringComparison.Ordinal);
        }

        [Fact]
        public void Serialize_escapes_the_line_separators_that_one_live_page_contains()
        {
            // U+2028/U+2029 terminate a line in JavaScript: Newtonsoft escapes them, JSON.stringify does
            // not. The seeding script has to escape them too, or that one page never stops looking changed.
            var page = new JObject { ["text"] = $"a{LineSeparator}b{ParagraphSeparator}c" };

            var json = PageJson.Serialize(page);

            Assert.DoesNotContain(LineSeparator.ToString(), json, StringComparison.Ordinal);
            Assert.DoesNotContain(ParagraphSeparator.ToString(), json, StringComparison.Ordinal);
            Assert.Contains("\\u2028", json, StringComparison.Ordinal);
            Assert.Contains("\\u2029", json, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("a\r\nb", "a\nb")]   // the environment stores most pages this way
        [InlineData("a\rb", "a\nb")]     // a lone CR would otherwise survive the CRLF pass
        [InlineData("a\nb", "a\nb")]
        public void Canonicalize_collapses_line_endings(string input, string expected)
        {
            Assert.Equal(expected, PageJson.Canonicalize(input));
        }

        [Fact]
        public void Canonicalize_strips_a_leading_bom()
        {
            Assert.Equal("{}", PageJson.Canonicalize(Bom + "{}"));
        }

        [Fact]
        public void Canonicalize_passes_null_and_empty_through()
        {
            Assert.Null(PageJson.Canonicalize(null));
            Assert.Equal(string.Empty, PageJson.Canonicalize(string.Empty));
        }

        [Fact]
        public void The_same_page_stored_with_crlf_and_with_lf_is_not_a_change()
        {
            const string lf = "{\n  \"a\": 1\n}";
            const string crlf = "{\r\n  \"a\": 1\r\n}";

            Assert.True(PageJson.AreSame(lf, crlf));
        }

        [Fact]
        public void A_missing_page_is_not_an_empty_page()
        {
            Assert.False(PageJson.AreSame(null, string.Empty));
            Assert.False(PageJson.AreSame(string.Empty, null));
            Assert.True(PageJson.AreSame(null, null));
        }

        [Fact]
        public void Different_content_is_a_change()
        {
            Assert.False(PageJson.AreSame("{\"a\":1}", "{\"a\":2}"));
        }

        [Fact]
        public void Encoding_does_not_emit_a_bom()
        {
            Assert.Empty(PageJson.Encoding.GetPreamble());
        }
    }
}
