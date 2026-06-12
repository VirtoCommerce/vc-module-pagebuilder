using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetReferenceMatcherTests
{
    [Theory]
    [InlineData("/assets/stores/B2B-store/Page%20Builder/2222/hero.png", "/stores/B2B-store/Page Builder/2222/hero.png")]
    [InlineData("https://localhost:5001/assets/stores/B2B-store/Page%20Builder/2222/hero.png?t=1", "/stores/B2B-store/Page Builder/2222/hero.png")]
    [InlineData("/stores/B2B-store/Page Builder/2222/hero.png", "/stores/B2B-store/Page Builder/2222/hero.png")]
    public void NormalizeAssetUrl_RemovesAssetsPrefixAndQuery(string value, string expected)
    {
        var actual = PageBuilderAssetReferenceMatcher.NormalizeAssetUrl(value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizeAssetUrl_KeepsInvalidEscapeSequences()
    {
        var actual = PageBuilderAssetReferenceMatcher.NormalizeAssetUrl("/assets/stores/B2B-store/Page Builder/2222/100% organic.png");

        Assert.Equal("/stores/B2B-store/Page Builder/2222/100% organic.png", actual);
    }

    [Fact]
    public void NormalizeAssetUrls_IgnoresEmptyValuesAndCollapsesDuplicates()
    {
        var actual = PageBuilderAssetReferenceMatcher.NormalizeAssetUrls([
            null,
            "",
            "/assets/stores/B2B-store/Page%20Builder/2222/hero.png",
            "/stores/B2B-store/Page Builder/2222/hero.png"
        ]);

        var reference = Assert.Single(actual);
        Assert.Equal("/stores/B2B-store/Page Builder/2222/hero.png", reference.Key);
        Assert.Equal("/assets/stores/B2B-store/Page%20Builder/2222/hero.png", reference.Value);
    }

    [Fact]
    public void ExtractReferences_ReturnsNormalizedAssetUrlsFromJsonStrings()
    {
        var content = """
            {
              "image": "/assets/stores/B2B-store/Page%20Builder/2222/hero%20banner.png?t=1",
              "html": "<img src=/assets/stores/B2B-store/Page%20Builder/2222/icon.png/>",
              "external": "https://localhost:5001/assets/stores/B2B-store/Page%20Builder/2222/absolute.png"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/2222/hero banner.png", result);
        Assert.Contains("/stores/B2B-store/Page Builder/2222/icon.png", result);
        Assert.Contains("/stores/B2B-store/Page Builder/2222/absolute.png", result);
    }

    [Fact]
    public void ExtractReferences_ReturnsMixedEncodedAssetUrl()
    {
        var content = """
            {
              "image": "/assets/stores/B2B-store/Page Builder/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-04-04%20122937.png"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/\u0421\u043D\u0438\u043C\u043E\u043A \u044D\u043A\u0440\u0430\u043D\u0430 2025-04-04 122937.png", result);
    }

    [Fact]
    public void ExtractReferences_ReturnsUnquotedAssetUrlWithSpaces()
    {
        var content = """
            {
              "html": "<img src=/assets/stores/B2B-store/Page Builder/Снимок экрана 2025-04-04 122937.png>"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/Снимок экрана 2025-04-04 122937.png", result);
        Assert.DoesNotContain("/stores/B2B-store/Page", result);
    }

    [Fact]
    public void ExtractReferences_UsesRawFallbackForInvalidJson()
    {
        var content = "broken /assets/stores/B2B-store/Page%20Builder/2222/hero.png json";

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/2222/hero.png", result);
    }

    [Fact]
    public void ExtractReferences_ReturnsAssetUrlsFromCssAndHtmlFragments()
    {
        var content = """
            {
              "style": "background-image: url(\"https://localhost:5001/assets/stores/B2B-store/Page%20Builder/2222/hero.png?t=1\")",
              "html": "<img src=/assets/stores/B2B-store/Page%20Builder/2222/icon.png/>"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/2222/hero.png", result);
        Assert.Contains("/stores/B2B-store/Page Builder/2222/icon.png", result);
    }

    [Fact]
    public void ExtractReferences_ReturnsQuotedHtmlAssetUrlsWithCommonFileNameCharacters()
    {
        var content = """
            {
              "html": "<img src=\"/assets/stores/B2B-store/Page%20Builder/2222/hero(1)[mobile]=wide.png\">"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/2222/hero(1)[mobile]=wide.png", result);
    }

    [Fact]
    public void ExtractReferences_KeepsCommaInsideQuotedAssetUrl()
    {
        var content = """
            {
              "html": "<img src=\"/assets/stores/B2B-store/Page%20Builder/2222/hero,desktop.png\">"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/2222/hero,desktop.png", result);
    }

    [Fact]
    public void ExtractReferences_ReturnsSrcsetAssetUrls()
    {
        var content = """
            {
              "html": "<img srcset=\"/assets/stores/B2B-store/Page%20Builder/2222/small.png 480w, /assets/stores/B2B-store/Page%20Builder/2222/large.png 960w\">"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/2222/small.png", result);
        Assert.Contains("/stores/B2B-store/Page Builder/2222/large.png", result);
    }

    [Fact]
    public void ExtractReferences_ReturnsAssetUrlInsideQueryParameter()
    {
        var content = """
            {
              "link": "/preview?image=/assets/stores/B2B-store/Page%20Builder/2222/hero.png&width=1200"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Contains("/stores/B2B-store/Page Builder/2222/hero.png", result);
        Assert.DoesNotContain("/stores/B2B-store/Page Builder/2222/hero.png&width", result);
    }

    [Fact]
    public void ExtractReferences_IgnoresNonAssetUrls()
    {
        var content = """
            {
              "link": "https://example.com/products",
              "relative": "/images/local.png",
              "text": "website, mobile app, marketplaces, social commerce, stores/POS, and contact centre"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractReferences_DoesNotScanJsonPropertyNames()
    {
        var content = """
            {
              "/assets/stores/B2B-store/Page%20Builder/2222/property-name.png": "not a reference"
            }
            """;

        var result = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        Assert.Empty(result);
    }
}
