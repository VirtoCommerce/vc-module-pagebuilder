using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class ThemePaletteServiceTests
{
    private static readonly IList<PaletteContrastPair> DefaultPairs = new DefaultPairsPaletteService().Pairs;

    private readonly ThemePaletteService _service = new();

    [Theory]
    [InlineData("#767676", "#ffffff", 4.54)]
    [InlineData("#000000", "#ffffff", 21)]
    [InlineData("#ffffff", "#ffffff", 1)]
    [InlineData("#777777", "#ffffff", 4.47)]
    [InlineData("#f99e24", "#ffffff", 2.11)]
    [InlineData("#0000ff", "#ffffff", 8.59)]
    public void GetContrastRatio_KnownColors_MatchesWcag(string first, string second, double expected)
    {
        Assert.True(PaletteColor.TryParse(first, out var a));
        Assert.True(PaletteColor.TryParse(second, out var b));

        var ratio = ColorContrast.GetContrastRatio(a, b);

        Assert.Equal(expected, ratio, 0.01);
        Assert.Equal(ratio, ColorContrast.GetContrastRatio(b, a));
    }

    [Theory]
    [InlineData("#FFF", "#ffffff")]
    [InlineData("#1b789B", "#1b789b")]
    [InlineData(" #abc ", "#aabbcc")]
    public void TryParse_HexColor_ReadsIt(string value, string expected)
    {
        Assert.True(PaletteColor.TryParse(value, out var color));
        Assert.Equal(expected, color.ToHex());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ffffff")]
    [InlineData("#ffff")]
    [InlineData("#gggggg")]
    [InlineData("#ffffff80")]
    [InlineData("rgb(0,0,0)")]
    public void TryParse_NotAHexColor_ReturnsFalse(string value)
    {
        Assert.False(PaletteColor.TryParse(value, out _));
    }

    [Theory]
    [InlineData("#1b789b")]
    [InlineData("#de3131")]
    [InlineData("#ffd700")]
    [InlineData("#808080")]
    public void FromHsl_ToHsl_RoundTrips(string value)
    {
        PaletteColor.TryParse(value, out var color);
        var (hue, saturation, lightness) = color.ToHsl();

        Assert.Equal(color, PaletteColor.FromHsl(hue, saturation, lightness));
    }

    [Theory]
    [InlineData("#fafafa", null)]
    [InlineData("#fff59d", null)]
    [InlineData("#0a0a0a", null)]
    [InlineData("#1a0033", null)]
    [InlineData("#ff0000", null)]
    [InlineData("#00ff00", null)]
    [InlineData("#0000ff", null)]
    [InlineData("#ffd700", null)]
    [InlineData("#808080", null)]
    [InlineData("#f99e24", null)]
    [InlineData("#ffffff", "#000000")]
    [InlineData("#fafafa", "#fff59d")]
    [InlineData("#0a0a0a", "#1a0033")]
    [InlineData("#ff0000", "#00ff00")]
    [InlineData("#ffd700", "#808080")]
    [InlineData("#f99e24", "#1b789b")]
    [InlineData("#00ffff", "#ff00ff")]
    [InlineData("#1b5e20", "#0d47a1")]
    [InlineData("#767676", "#777777")]
    public void GeneratePalette_AnyColors_PassesEveryPairInBothSchemes(string primary, string secondary)
    {
        var result = _service.GeneratePalette(Request(primary, secondary));

        Assert.Empty(result.Failures);

        var check = _service.CheckPalette(new ThemePaletteCheckRequest
        {
            Palette = new ThemePalette { Light = result.Light, Dark = result.Dark },
            Tokens = Tokens(),
        });
        Assert.Empty(check.Failures);
        Assert.Equal(0, check.Unchecked);
    }

    [Fact]
    public void GeneratePalette_StorefrontTokens_ColorsEveryAnnouncedToken()
    {
        var result = _service.GeneratePalette(Request("#1b789b", "#f99e24"));

        Assert.Equal(StorefrontPaletteTokens.Light.Order(), result.Light.Keys.Order());
        Assert.Equal(StorefrontPaletteTokens.Dark.Order(), result.Dark.Keys.Order());
        Assert.All(result.Light.Values.Concat(result.Dark.Values), x => Assert.True(PaletteColor.TryParse(x, out _), x));
    }

    [Fact]
    public void GeneratePalette_ColorThatPasses_KeepsItAsShade500()
    {
        var result = _service.GeneratePalette(Request("#1b789b", "#2e7d32"));

        Assert.Equal("#1b789b", result.Light["color_primary_500"]);
        Assert.Equal("#2e7d32", result.Light["color_accent_500"]);
        Assert.Equal("#2e7d32", result.Light["color_secondary_500"]);
        Assert.Equal("#1b789b", result.Dark["color_primary_500"]);
    }

    [Fact]
    public void GeneratePalette_LightColorUnderWhiteText_DarkensShade500AndKeepsTheScaleInOrder()
    {
        var result = _service.GeneratePalette(Request("#ffd700", null));

        PaletteColor.TryParse(result.Light["color_primary_500"], out var primary);
        Assert.True(ColorContrast.GetContrastRatio(primary, PaletteColor.White) >= 4.5);

        var lightness = new[] { 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950 }
            .Select(x => { PaletteColor.TryParse(result.Light[$"color_primary_{x}"], out var color); return color.ToHsl().Lightness; })
            .ToList();
        Assert.Equal(lightness.OrderDescending(), lightness);
    }

    [Fact]
    public void GeneratePalette_OneColor_SecondaryIsAMutedPrimaryAndAccentIsThePrimary()
    {
        var result = _service.GeneratePalette(Request("#1b789b", null));

        Assert.Equal("#1b789b", result.Light["color_accent_500"]);
        PaletteColor.TryParse(result.Light["color_secondary_500"], out var secondary);
        PaletteColor.TryParse(result.Light["color_primary_500"], out var primary);
        Assert.True(secondary.ToHsl().Saturation <= 0.19);
        Assert.Equal(primary.ToHsl().Hue, secondary.ToHsl().Hue, 1d);
    }

    [Fact]
    public void GeneratePalette_DarkScheme_PicksSolidTextByTheFill()
    {
        var result = _service.GeneratePalette(Request("#f99e24", "#1a237e"));

        PaletteColor.TryParse(result.Dark["color_vc_text_solid_primary"], out var onOrange);
        Assert.True(ColorContrast.GetRelativeLuminance(onOrange) < 0.1);
        Assert.Equal("#ffffff", result.Dark["color_vc_text_solid_secondary"]);
    }

    [Fact]
    public void GeneratePalette_UnknownToken_LeavesItOut()
    {
        var request = Request("#1b789b", null);
        request.Tokens.Light = [.. StorefrontPaletteTokens.Light, "color_brand_glow"];

        var result = _service.GeneratePalette(request);

        Assert.DoesNotContain("color_brand_glow", result.Light.Keys);
    }

    [Fact]
    public void GetContrastPairs_ChangedByAnOverride_LeavesTheNextCallsPairsAlone()
    {
        var service = new DefaultPairsPaletteService();
        var before = service.Pairs[0].MinimumRatio;

        service.Pairs[0].MinimumRatio += 1;

        Assert.Equal(before, service.Pairs[0].MinimumRatio);
    }

    [Fact]
    public void GeneratePalette_ShadeBeyondAnyNumber_LeavesItOut()
    {
        var request = Request("#1b789b", null);
        request.Tokens.Light = [.. StorefrontPaletteTokens.Light, "color_primary_99999999999"];

        var result = _service.GeneratePalette(request);

        Assert.DoesNotContain("color_primary_99999999999", result.Light.Keys);
    }

    [Fact]
    public void CheckPalette_PresetPrimaryUnderWhite_ReturnsTheFailingPairsWithTheTokensShown()
    {
        var result = _service.CheckPalette(Check(new Dictionary<string, string>
        {
            ["color_primary_500"] = "#f99e24",
            ["color_additional_50"] = "#ffffff",
        }));

        var solid = Assert.Single(result.Failures, x => x.Pair == "button-solid-primary");
        Assert.Equal("light", solid.Scheme);
        Assert.Equal("color_additional_50", solid.Text);
        Assert.Equal("color_primary_500", solid.Background);
        Assert.Equal("#ffffff", solid.TextColor);
        Assert.Equal("#f99e24", solid.BackgroundColor);
        Assert.Equal(2.11, solid.Ratio);
        Assert.Equal(4.5, solid.MinimumRatio);
        Assert.Contains(result.Failures, x => x.Pair == "button-outline-primary" && x.Text == "color_primary_500" && x.Background == "color_additional_50");
        // The rest of the pairs need colors the palette leaves to the preset.
        Assert.Equal(DefaultPairs.Count - 3, result.Unchecked);
    }

    [Theory]
    [InlineData("#767676", false)]
    [InlineData("#777777", true)]
    public void CheckPalette_TextAtTheAaLimit_FailsBelow4Point5(string text, bool fails)
    {
        var result = _service.CheckPalette(Check(new Dictionary<string, string>
        {
            ["color_neutral_950"] = text,
            ["color_neutral_50"] = "#ffffff",
        }));

        Assert.Equal(fails, result.Failures.Any(x => x.Pair == "body-text"));
    }

    [Theory]
    [InlineData("#949494", false)]
    [InlineData("#959595", true)]
    public void CheckPalette_IconAtTheLimit_FailsBelow3(string background, bool fails)
    {
        var result = _service.CheckPalette(Check(new Dictionary<string, string>
        {
            ["color_additional_50"] = "#ffffff",
            ["color_secondary_500"] = background,
        }));

        var failure = result.Failures.SingleOrDefault(x => x.Pair == "shape-icon");
        Assert.Equal(fails, failure != null);
        if (failure != null)
        {
            Assert.Equal(3, failure.MinimumRatio);
        }
    }

    [Fact]
    public void CheckPalette_AnnouncedTokenLeftOut_DoesNotCheckItsPair()
    {
        // The dark preset has its own header text color, which the server does not know.
        var result = _service.CheckPalette(new ThemePaletteCheckRequest
        {
            Palette = new ThemePalette
            {
                Light = new Dictionary<string, string>(),
                Dark = new Dictionary<string, string> { ["color_header_top_bg"] = "#ffffff", ["color_additional_50"] = "#ffffff" },
            },
            Tokens = Tokens(),
        });

        Assert.Empty(result.Failures);
        Assert.Equal(DefaultPairs.Count, result.Unchecked);
    }

    [Fact]
    public void CheckPalette_TokenTheStorefrontDoesNotHave_ReadsItsFallback()
    {
        // Light has no header tokens, so the storefront's stylesheet falls back to the secondary and the white.
        var result = _service.CheckPalette(Check(new Dictionary<string, string>
        {
            ["color_secondary_900"] = "#eeeeee",
            ["color_additional_50"] = "#ffffff",
        }));

        Assert.Contains(result.Failures, x => x.Pair == "header-top-text" && x.Text == "color_additional_50" && x.Background == "color_secondary_900");
    }

    [Fact]
    public void CheckPalette_PaletteHasTheTokenItself_UsesItOverTheFallback()
    {
        var result = _service.CheckPalette(Check(new Dictionary<string, string>
        {
            ["color_secondary_900"] = "#eeeeee",
            ["color_additional_50"] = "#ffffff",
            ["color_header_top_text"] = "#000000",
        }));

        Assert.DoesNotContain(result.Failures, x => x.Pair == "header-top-text");
    }

    [Fact]
    public void CheckPalette_ValueIsNotAColor_DoesNotCheckItsPairs()
    {
        var result = _service.CheckPalette(Check(new Dictionary<string, string>
        {
            ["color_primary_500"] = "orange",
            ["color_additional_50"] = "#ffffff",
        }));

        Assert.DoesNotContain(result.Failures, x => x.Pair.EndsWith("-primary"));
        Assert.Equal(DefaultPairs.Count, result.Unchecked);
    }

    [Fact]
    public void ContrastPairs_EveryToken_IsOneTheStorefrontStylesheetsName()
    {
        var known = StorefrontPaletteTokens.Dark.Concat(StorefrontPaletteTokens.Light).ToHashSet();

        Assert.All(DefaultPairs, pair =>
        {
            Assert.True(pair.MinimumRatio is 4.5 or 3, pair.Name);
            foreach (var reference in new[] { pair.Text, pair.Background })
            {
                Assert.True(reference.Fallback == null || StorefrontPaletteTokens.Light.Contains(reference.Fallback), reference.Fallback);
                Assert.True(known.Contains(reference.Token)
                    || reference.Token.StartsWith("color_vc_background_outline_")
                    || reference.Token.StartsWith("color_vc_background_surface_"), reference.Token);
            }
        });
        Assert.Equal(DefaultPairs.Count, DefaultPairs.Select(x => x.Name).Distinct().Count());
    }

    [Fact]
    public void GeneratePalette_PairOnWhite_MovesTheShadeNeverTheWhite()
    {
        var service = new OnePairPaletteService(Pair("color_additional_50", "color_primary_500"));

        var result = service.GeneratePalette(Request("#fafafa", null));

        Assert.Equal("#ffffff", result.Light["color_additional_50"]);
        Assert.NotEqual("#fafafa", result.Light["color_primary_500"]);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void GeneratePalette_EitherSideCouldMove_MovesTheOtherShadeBeforeTheBrandColor()
    {
        // Darkening the primary is the smaller change, but lightening the secondary's 200 keeps the brand color.
        var service = new OnePairPaletteService(Pair("color_primary_500", "color_secondary_200"));

        var result = service.GeneratePalette(Request("#6a6a6a", "#6a6a6a"));

        Assert.Equal("#6a6a6a", result.Light["color_primary_500"]);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void GeneratePalette_FixedBaseColorOverridden_BuildsTheScaleFromIt()
    {
        var result = new NavyInfoPaletteService().GeneratePalette(Request("#6a6a6a", "#6a6a6a"));

        Assert.Equal("#000080", result.Light["color_info_500"]);
    }

    private static PaletteContrastPair Pair(string text, string background) =>
        new()
        {
            Name = "custom",
            Text = new PaletteTokenReference { Token = text },
            Background = new PaletteTokenReference { Token = background },
            MinimumRatio = 4.5,
        };

    private sealed class OnePairPaletteService(PaletteContrastPair pair) : ThemePaletteService
    {
        protected override IList<PaletteContrastPair> GetContrastPairs() => [pair];
    }

    private sealed class NavyInfoPaletteService : ThemePaletteService
    {
        protected override IDictionary<string, PaletteColor> GetFixedBaseColors()
        {
            var colors = base.GetFixedBaseColors();
            colors["info"] = new PaletteColor(0x00, 0x00, 0x80);
            return colors;
        }
    }

    private sealed class DefaultPairsPaletteService : ThemePaletteService
    {
        public IList<PaletteContrastPair> Pairs => GetContrastPairs();
    }

    private static ThemePaletteGenerationRequest Request(string primary, string secondary) =>
        new() { Primary = primary, Secondary = secondary, Tokens = Tokens() };

    private static ThemePaletteCheckRequest Check(Dictionary<string, string> light) =>
        new() { Palette = new ThemePalette { Light = light }, Tokens = Tokens() };

    private static ThemePaletteTokens Tokens() =>
        new() { Light = [.. StorefrontPaletteTokens.Light], Dark = [.. StorefrontPaletteTokens.Dark] };
}
