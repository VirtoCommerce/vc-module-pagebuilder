using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class PageEnvelopeValidatorTests
    {
        [Fact]
        public void Validate_ValidEnvelope_NoErrors()
        {
            var page = JToken.Parse("""
                {
                  "settings": { "type": "settings", "name": "Test" },
                  "content": [
                    { "type": "hero", "id": "a1" },
                    { "type": "text", "id": "a2" }
                  ]
                }
                """);

            Assert.Empty(PageEnvelopeValidator.Validate(page));
        }

        [Fact]
        public void Validate_LegacyFlatArray_NoErrors()
        {
            var page = JToken.Parse("""
                [
                  { "type": "settings", "name": "Test" },
                  { "type": "hero", "id": "a1" }
                ]
                """);

            Assert.Empty(PageEnvelopeValidator.Validate(page));
        }

        [Fact]
        public void Validate_WrongSettingsType_ReportsError()
        {
            var page = JToken.Parse("""{ "settings": { "type": "WRONG" }, "content": [] }""");

            var errors = PageEnvelopeValidator.Validate(page);

            Assert.Contains(errors, e => e.Contains("settings.type"));
        }

        [Fact]
        public void Validate_MissingContent_ReportsError()
        {
            var page = JToken.Parse("""{ "settings": { "type": "settings" } }""");

            var errors = PageEnvelopeValidator.Validate(page);

            Assert.Contains(errors, e => e.Contains("content array"));
        }

        [Fact]
        public void Validate_DuplicateSectionIds_ReportsError()
        {
            var page = JToken.Parse("""
                {
                  "settings": { "type": "settings" },
                  "content": [
                    { "type": "hero", "id": "dup" },
                    { "type": "text", "id": "dup" }
                  ]
                }
                """);

            var errors = PageEnvelopeValidator.Validate(page);

            Assert.Contains(errors, e => e.Contains("duplicate id \"dup\""));
        }

        [Fact]
        public void Validate_SectionWithoutType_ReportsError()
        {
            var page = JToken.Parse("""
                { "settings": { "type": "settings" }, "content": [ { "id": "a1" } ] }
                """);

            var errors = PageEnvelopeValidator.Validate(page);

            Assert.Contains(errors, e => e.Contains("missing type"));
        }

        [Fact]
        public void Validate_NullOrScalar_ReportsError()
        {
            Assert.NotEmpty(PageEnvelopeValidator.Validate(JValue.CreateNull()));
            Assert.NotEmpty(PageEnvelopeValidator.Validate(new JValue("just a string")));
        }
    }
}
