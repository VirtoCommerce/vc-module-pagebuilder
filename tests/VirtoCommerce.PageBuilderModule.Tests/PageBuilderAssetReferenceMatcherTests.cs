using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetReferenceMatcherTests
{
    private readonly PageBuilderAssetReferenceMatcher _matcher = new();

    [Theory]
    [InlineData("/assets/stores/B2B-store/Page%20Builder/2222/hero.png", "/stores/B2B-store/Page Builder/2222/hero.png")]
    [InlineData("https://localhost:5001/assets/stores/B2B-store/Page%20Builder/2222/hero.png?t=1", "/stores/B2B-store/Page Builder/2222/hero.png")]
    [InlineData("/stores/B2B-store/Page Builder/2222/hero.png", "/stores/B2B-store/Page Builder/2222/hero.png")]
    public void NormalizeAssetUrl_RemovesAssetsPrefixAndQuery(string value, string expected)
    {
        var actual = _matcher.NormalizeAssetUrl(value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizeAssetUrl_KeepsInvalidEscapeSequences()
    {
        var actual = _matcher.NormalizeAssetUrl("/assets/stores/B2B-store/Page Builder/2222/100% organic.png");

        Assert.Equal("/stores/B2B-store/Page Builder/2222/100% organic.png", actual);
    }

    [Fact]
    public void NormalizeAssetUrls_IgnoresEmptyValuesAndCollapsesDuplicates()
    {
        var actual = _matcher.NormalizeAssetUrls([
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
    public void FindReferences_MatchesEncodedAndDecodedAssetUrls()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero banner.png");
        var content = """
            {
              "content": [
                {
                  "settings": {
                    "image": "/assets/stores/B2B-store/Page%20Builder/2222/hero%20banner.png"
                  }
                }
              ]
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }

    [Fact]
    public void FindReferences_MatchesAbsoluteAssetUrls()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero.png");
        var content = """
            {
              "image": "https://localhost:5001/assets/stores/B2B-store/Page%20Builder/2222/hero.png?t=638000000000000000"
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }

    [Fact]
    public void FindReferences_MatchesMixedEncodedJsonAssetUrl()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-04-04%20122937.png");
        var content = """
            {
              "image": "/assets/stores/B2B-store/Page Builder/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-04-04%20122937.png"
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }

    [Fact]
    public void FindReferences_UsesRawFallbackForInvalidJson()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero.png");
        var content = $"broken /assets/stores/B2B-store/Page%20Builder/2222/hero.png json";

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }

    [Fact]
    public void FindReferences_DoesNotMatchAssetUrlAsSubstringOfAnotherPath()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero.png");
        var content = """
            {
              "image": "/assets/stores/B2B-store/Page%20Builder/2222/hero.png.backup",
              "anotherImage": "/assets/stores/B2B-store/Page%20Builder/2222/hero.png2",
              "nestedImage": "/archive/stores/B2B-store/Page Builder/2222/hero.png"
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.DoesNotContain(assetUrl, result);
    }

    [Fact]
    public void FindReferences_MatchesAssetUrlInsideAbsoluteCssUrl()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero.png");
        var content = """
            {
              "style": "background-image: url(\"https://localhost:5001/assets/stores/B2B-store/Page%20Builder/2222/hero.png?t=1\")"
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }

    [Fact]
    public void FindReferences_MatchesUnquotedCssUrl()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero.png");
        var content = """
            {
              "style": "background-image: url(/assets/stores/B2B-store/Page%20Builder/2222/hero.png)"
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }

    [Fact]
    public void FindReferences_MatchesUnquotedHtmlAttributeUrl()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero.png");
        var content = """
            {
              "html": "<img src=/assets/stores/B2B-store/Page%20Builder/2222/hero.png>"
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }

    [Fact]
    public void FindReferences_MatchesAssetUrlBeforeQueryParameterSeparator()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero.png");
        var content = """
            {
              "link": "/preview?image=/assets/stores/B2B-store/Page%20Builder/2222/hero.png&width=1200"
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }

    [Fact]
    public void FindReferences_MatchesAssetUrlInsideSelfClosingHtmlTag()
    {
        var assetUrl = _matcher.NormalizeAssetUrl("/stores/B2B-store/Page Builder/2222/hero.png");
        var content = """
            {
              "html": "<img src=/assets/stores/B2B-store/Page%20Builder/2222/hero.png/>"
            }
            """;

        var result = _matcher.FindReferences(content, [assetUrl]);

        Assert.Contains(assetUrl, result);
    }
}
