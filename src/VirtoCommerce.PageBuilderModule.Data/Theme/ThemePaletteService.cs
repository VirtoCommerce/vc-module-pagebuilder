using System.Globalization;
using System.Text.RegularExpressions;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Palette;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemePaletteService : IThemePaletteService
{
    private const int MaxAdjustmentRounds = 8;
    private const int SearchSteps = 24;
    private const double ShadeOrderStep = 0.03;
    private const double BaseShadePenalty = 0.5;
    private const double ShortfallTolerance = 1e-9;
    private const double LightestLightness = 0.985;
    private const double DarkestLightness = 0.04;

    private static readonly int[] Shades = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    private static readonly Regex ShadeToken = new(@"^color_([a-z]+)_(\d+)$", RegexOptions.None, TimeSpan.FromSeconds(1));
    private static readonly Regex VcToken = new(@"^color_vc_([a-z]+_[a-z]+)_([a-z]+)$", RegexOptions.None, TimeSpan.FromSeconds(1));

    // the default preset's neutral and status colors
    private static readonly Dictionary<string, PaletteColor> FixedBaseColors = new()
    {
        ["neutral"] = new PaletteColor(0x73, 0x73, 0x73),
        ["success"] = new PaletteColor(0x3e, 0x84, 0x5b),
        ["warning"] = new PaletteColor(0xfc, 0x9e, 0x00),
        ["danger"] = new PaletteColor(0xde, 0x31, 0x31),
        ["info"] = new PaletteColor(0x2b, 0x7e, 0xa8),
    };

    private static readonly PaletteColor DarkSurface = new(0x0a, 0x09, 0x0b);
    private static readonly PaletteColor DarkText = new(0xec, 0xea, 0xef);
    private static readonly PaletteColor SolidDarkText = new(0x16, 0x17, 0x1b);

    // fractions of the way from the base color (500) to the lightest and the darkest lightness
    private static readonly Dictionary<int, double> LighterShades = new() { [400] = 0.3, [300] = 0.5, [200] = 0.7, [100] = 0.85, [50] = 0.97 };
    private static readonly Dictionary<int, double> DarkerShades = new() { [600] = 0.2, [700] = 0.38, [800] = 0.55, [900] = 0.72, [950] = 0.85 };

    // VcButton tokens by role, "{0}" is the color: light as the storefront's fallbacks, dark as its default dark preset
    private static readonly Dictionary<string, string> LightRoles = new()
    {
        ["background_solid"] = "color_{0}_500",
        ["border_solid"] = "color_{0}_500",
        ["background_soft"] = "color_{0}_100",
        ["border_soft"] = "color_{0}_100",
        ["text_soft"] = "color_{0}_800",
        ["icon_soft"] = "color_{0}_800",
        ["background_outline"] = PaletteContrastPairs.AdditionalLight,
        ["border_outline"] = "color_{0}_500",
        ["text_outline"] = "color_{0}_700",
        ["icon_outline"] = "color_{0}_700",
        ["background_surface"] = PaletteContrastPairs.AdditionalLight,
        ["border_surface"] = PaletteContrastPairs.AdditionalLight,
        ["text_surface"] = "color_{0}_700",
        ["icon_surface"] = "color_{0}_700",
        ["text_ghost"] = "color_{0}_700",
        ["icon_ghost"] = "color_{0}_700",
        ["background_tonal"] = "color_{0}_100",
        ["border_tonal"] = "color_{0}_500",
        ["text_tonal"] = "color_{0}_800",
        ["icon_tonal"] = "color_{0}_800",
    };

    private static readonly Dictionary<string, string> DarkRoles = new(LightRoles)
    {
        ["text_outline"] = "color_{0}_800",
        ["icon_outline"] = "color_{0}_800",
        ["text_surface"] = "color_{0}_800",
        ["icon_surface"] = "color_{0}_800",
        ["text_ghost"] = "color_{0}_800",
        ["icon_ghost"] = "color_{0}_800",
    };

    // the other tokens: light as the storefront's fallbacks, dark as its default dark preset
    private static readonly Dictionary<string, (string Light, string Dark)> NamedTokens = new()
    {
        ["color_body_bg"] = ("color_neutral_50", "color_neutral_50"),
        ["color_body_text"] = ("color_neutral_950", "color_neutral_950"),
        ["color_link"] = ("color_accent_600", "color_accent_800"),
        ["color_link_hover"] = ("color_accent_700", "color_accent_900"),
        ["color_price"] = ("color_neutral_900", "color_neutral_900"),
        ["color_shape_icon_bg"] = ("color_secondary_500", "color_secondary_500"),
        ["color_shape_icon"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_empty_list_icon"] = ("color_primary_500", "color_primary_500"),
        ["color_mobile_search_bar_bg"] = (PaletteContrastPairs.AdditionalLight, "color_neutral_50"),
        ["color_mobile_menu_bg"] = ("color_secondary_900", "color_secondary_950"),
        ["color_mobile_menu_text"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_mobile_menu_link"] = ("color_secondary_200", "color_secondary_800"),
        ["color_mobile_menu_link_active"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_mobile_menu_icon"] = ("color_secondary_600", "color_secondary_500"),
        ["color_mobile_menu_icon_active"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_mobile_menu_navigation"] = ("color_primary_400", "color_primary_400"),
        ["color_mobile_menu_control"] = ("color_primary_500", "color_primary_500"),
        ["color_mobile_active_control"] = ("color_primary_500", "color_primary_500"),
        ["color_header_top_bg"] = ("color_secondary_900", "color_secondary_950"),
        ["color_header_top_text"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_header_top_link"] = ("color_accent_300", "color_accent_300"),
        ["color_header_top_link_hover"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_header_top_link_active"] = ("color_accent_300", "color_accent_300"),
        ["color_header_bottom_bg"] = (PaletteContrastPairs.AdditionalLight, "color_neutral_50"),
        ["color_header_bottom_text"] = ("color_neutral_900", "color_neutral_900"),
        ["color_header_bottom_link"] = ("color_secondary_700", "color_neutral_700"),
        ["color_header_bottom_link_hover"] = ("color_neutral_900", "color_neutral_950"),
        ["color_header_bottom_link_active"] = ("color_neutral_900", "color_neutral_950"),
        ["color_footer_top_bg"] = ("color_secondary_900", "color_secondary_950"),
        ["color_footer_top_text"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_footer_top_link"] = ("color_neutral_400", "color_neutral_600"),
        ["color_footer_top_link_hover"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_footer_top_link_active"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_footer_bottom_bg"] = ("color_secondary_950", PaletteContrastPairs.AdditionalLight),
        ["color_footer_bottom_text"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_footer_bottom_link"] = ("color_accent_300", "color_accent_300"),
        ["color_footer_bottom_link_hover"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
        ["color_footer_bottom_link_active"] = (PaletteContrastPairs.AdditionalLight, PaletteContrastPairs.AdditionalDark),
    };

    public virtual ThemePaletteGenerationResult GeneratePalette(ThemePaletteGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseColors = GetBaseColors(request);
        var pairs = GetContrastPairs();
        var lightTokens = request.Tokens.Light.ToHashSet();
        var darkTokens = request.Tokens.Dark.ToHashSet();

        var light = GenerateColors(baseColors, lightTokens, Light, pairs);
        var dark = GenerateColors(baseColors, darkTokens, Dark, pairs);

        var result = AbstractTypeFactory<ThemePaletteGenerationResult>.TryCreateInstance();
        result.Light = ToHex(light);
        result.Dark = ToHex(dark);
        result.Failures = [.. GetFailures(light, lightTokens, Light, pairs, out _), .. GetFailures(dark, darkTokens, Dark, pairs, out _)];

        return result;
    }

    public virtual ThemePaletteCheckResult CheckPalette(ThemePaletteCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pairs = GetContrastPairs();
        var result = AbstractTypeFactory<ThemePaletteCheckResult>.TryCreateInstance();

        foreach (var (scheme, palette, tokens) in new[] { (Light, request.Palette.Light, request.Tokens.Light), (Dark, request.Palette.Dark, request.Tokens.Dark) })
        {
            if (palette == null || palette.Count == 0)
            {
                continue;
            }

            var colors = palette.ToDictionary(x => x.Key, x => PaletteColor.TryParse(x.Value, out var color) ? color : (PaletteColor?)null);
            var failures = GetFailures(colors, tokens.ToHashSet(), scheme, pairs, out var uncheckedPairs);
            foreach (var failure in failures)
            {
                result.Failures.Add(failure);
            }
            result.Unchecked += uncheckedPairs;
        }

        return result;
    }

    protected virtual IList<PaletteContrastPair> GetContrastPairs() => [.. PaletteContrastPairs.All];

    /// <summary>The base color (shade 500) of each color family.</summary>
    protected virtual IDictionary<string, PaletteColor> GetBaseColors(ThemePaletteGenerationRequest request)
    {
        PaletteColor.TryParse(request.Primary, out var primary);
        var colors = new Dictionary<string, PaletteColor>(FixedBaseColors) { ["primary"] = primary };

        if (PaletteColor.TryParse(request.Secondary, out var secondary))
        {
            colors["secondary"] = secondary;
            colors["accent"] = secondary;
        }
        else
        {
            var (hue, saturation, _) = primary.ToHsl();
            colors["secondary"] = PaletteColor.FromHsl(hue, Math.Min(saturation, 0.18), 0.45);
            colors["accent"] = primary;
        }

        return colors;
    }

    /// <summary>The shades 50–950 of a color family; dark ones invert unevenly, as the storefront's dark presets do.</summary>
    protected virtual IDictionary<int, PaletteColor> CreateScale(string family, PaletteColor baseColor, string scheme)
    {
        var (hue, saturation, lightness) = baseColor.ToHsl();
        var light = new Dictionary<int, PaletteColor> { [500] = baseColor };
        foreach (var (shade, fraction) in LighterShades)
        {
            light[shade] = PaletteColor.FromHsl(hue, saturation, lightness + ((LightestLightness - lightness) * fraction));
        }
        foreach (var (shade, fraction) in DarkerShades)
        {
            light[shade] = PaletteColor.FromHsl(hue, saturation, lightness - ((lightness - DarkestLightness) * fraction));
        }

        if (scheme == Light)
        {
            return light;
        }

        if (family == "neutral")
        {
            return Shades.ToDictionary(x => x, x => light[1000 - x]);
        }

        return new Dictionary<int, PaletteColor>
        {
            [50] = PaletteColor.FromHsl(hue, saturation * 0.4, 0.13),
            [100] = PaletteColor.FromHsl(hue, saturation * 0.45, 0.18),
            [200] = light[200],
            [300] = light[300],
            [400] = light[400],
            [500] = light[500],
            [600] = light[600],
            [700] = light[300],
            [800] = light[200],
            [900] = light[100],
            [950] = PaletteColor.FromHsl(hue, saturation * 0.5, 0.06),
        };
    }

    /// <summary>A token's color, or null for a token without a rule: it keeps the preset's color.</summary>
    protected virtual PaletteColor? CreateTokenColor(string token, string scheme, IDictionary<string, IDictionary<int, PaletteColor>> scales)
    {
        if (token == PaletteContrastPairs.AdditionalLight)
        {
            return scheme == Light ? PaletteColor.White : DarkSurface;
        }

        if (token == PaletteContrastPairs.AdditionalDark)
        {
            return scheme == Light ? PaletteColor.Black : DarkText;
        }

        var shadeMatch = ShadeToken.Match(token);
        if (shadeMatch.Success)
        {
            return scales.TryGetValue(shadeMatch.Groups[1].Value, out var scale)
                   && scale.TryGetValue(int.Parse(shadeMatch.Groups[2].Value, CultureInfo.InvariantCulture), out var color)
                ? color
                : null;
        }

        var source = GetSourceToken(token, scheme);
        return source == null ? null : CreateTokenColor(source, scheme, scales);
    }

    /// <summary>The text on a solid fill: white or near black, whichever contrasts more.</summary>
    protected virtual PaletteColor CreateSolidTextColor(PaletteColor background) =>
        ColorContrast.GetContrastRatio(PaletteColor.White, background) >= ColorContrast.GetContrastRatio(SolidDarkText, background)
            ? PaletteColor.White
            : SolidDarkText;

    /// <summary>Moves colors until every pair passes; a move is kept only when the pairs fall short by less in total.</summary>
    protected virtual void AdjustContrast(IDictionary<string, PaletteColor?> colors, ISet<string> tokens, IList<PaletteContrastPair> pairs)
    {
        for (var round = 0; round < MaxAdjustmentRounds; round++)
        {
            var changed = false;
            foreach (var pair in pairs)
            {
                var evaluation = Evaluate(pair, colors, tokens);
                if (evaluation is { Passes: false })
                {
                    changed |= TryAdjust(evaluation, pair.MinimumRatio, colors, tokens, pairs);
                }
            }

            if (!changed)
            {
                return;
            }
        }
    }

    /// <summary>What moving a token's color costs: its lightness change, more for a color's own shade 500.</summary>
    protected virtual double GetAdjustmentCost(string token, double lightnessChange) =>
        token.EndsWith("_500", StringComparison.Ordinal) ? lightnessChange + BaseShadePenalty : lightnessChange;

    /// <summary>Makes the light shades of each color go from light to dark again after the contrast moved some.</summary>
    protected virtual void KeepShadeOrder(IDictionary<string, PaletteColor?> colors, ISet<string> tokens, IList<PaletteContrastPair> pairs)
    {
        foreach (var family in PaletteContrastPairs.Families)
        {
            var middle = Array.IndexOf(Shades, 500);
            for (var i = middle + 1; i < Shades.Length; i++)
            {
                OrderShade(family, Shades[i], Shades[i - 1], darker: true, colors, tokens, pairs);
            }
            for (var i = middle - 1; i >= 0; i--)
            {
                OrderShade(family, Shades[i], Shades[i + 1], darker: false, colors, tokens, pairs);
            }
        }
    }

    /// <summary>The pairs the colors fail; <paramref name="uncheckedPairs"/> counts those a color is missing for.</summary>
    protected virtual IList<PaletteContrastFailure> GetFailures(IDictionary<string, PaletteColor?> colors, ISet<string> tokens, string scheme, IList<PaletteContrastPair> pairs, out int uncheckedPairs)
    {
        var failures = new List<PaletteContrastFailure>();
        uncheckedPairs = 0;

        foreach (var pair in pairs)
        {
            var evaluation = Evaluate(pair, colors, tokens);
            if (evaluation == null)
            {
                uncheckedPairs++;
                continue;
            }

            if (!evaluation.Passes)
            {
                var failure = AbstractTypeFactory<PaletteContrastFailure>.TryCreateInstance();
                failure.Scheme = scheme;
                failure.Pair = pair.Name;
                failure.Text = evaluation.TextToken;
                failure.Background = evaluation.BackgroundToken;
                failure.TextColor = evaluation.TextColor.ToHex();
                failure.BackgroundColor = evaluation.BackgroundColor.ToHex();
                failure.Ratio = Math.Floor(evaluation.Ratio * 100) / 100;
                failure.MinimumRatio = pair.MinimumRatio;
                failures.Add(failure);
            }
        }

        return failures;
    }

    /// <summary>The token whose color the storefront shows: the token, else its fallback when the storefront has no such token.</summary>
    protected virtual string ResolveToken(PaletteTokenReference reference, IDictionary<string, PaletteColor?> colors, ISet<string> tokens)
    {
        if (colors.ContainsKey(reference.Token))
        {
            return reference.Token;
        }

        // a token the storefront announced and the palette leaves out keeps the preset's color, which is not known here
        return !tokens.Contains(reference.Token) && reference.Fallback != null && colors.ContainsKey(reference.Fallback)
            ? reference.Fallback
            : null;
    }

    private Dictionary<string, PaletteColor?> GenerateColors(IDictionary<string, PaletteColor> baseColors, ISet<string> tokens, string scheme, IList<PaletteContrastPair> pairs)
    {
        var scales = baseColors.ToDictionary(x => x.Key, x => CreateScale(x.Key, x.Value, scheme));
        var colors = new Dictionary<string, PaletteColor?>();

        foreach (var token in tokens)
        {
            if (CreateTokenColor(token, scheme, scales) is { } color)
            {
                colors[token] = color;
            }
        }

        // the text on a solid fill follows the fill
        foreach (var token in tokens)
        {
            var match = VcToken.Match(token);
            if (match.Success && match.Groups[1].Value is "text_solid" or "icon_solid")
            {
                var family = match.Groups[2].Value;
                var background = FindColor(colors, $"color_vc_background_solid_{family}") ?? FindColor(colors, $"color_{family}_500");
                if (background != null)
                {
                    colors[token] = CreateSolidTextColor(background.Value);
                }
            }
        }

        AdjustContrast(colors, tokens, pairs);
        if (scheme == Light)
        {
            KeepShadeOrder(colors, tokens, pairs);
        }

        return colors;
    }

    private string GetSourceToken(string token, string scheme)
    {
        if (NamedTokens.TryGetValue(token, out var named))
        {
            return scheme == Light ? named.Light : named.Dark;
        }

        var match = VcToken.Match(token);
        var roles = scheme == Light ? LightRoles : DarkRoles;

        return match.Success && roles.TryGetValue(match.Groups[1].Value, out var source)
            ? string.Format(CultureInfo.InvariantCulture, source, match.Groups[2].Value)
            : null;
    }

    private bool TryAdjust(PairEvaluation evaluation, double minimumRatio, IDictionary<string, PaletteColor?> colors, ISet<string> tokens, IList<PaletteContrastPair> pairs)
    {
        var candidates = new[]
            {
                (Token: evaluation.TextToken, Color: evaluation.TextColor, Other: evaluation.BackgroundColor),
                (Token: evaluation.BackgroundToken, Color: evaluation.BackgroundColor, Other: evaluation.TextColor),
            }
            .Where(x => !IsFixed(x.Token))
            .SelectMany(x => FindContrastingColors(x.Color, x.Other, minimumRatio)
                .Select(candidate => (x.Token, Candidate: candidate.Color, Cost: GetAdjustmentCost(x.Token, candidate.LightnessChange))))
            .OrderBy(x => x.Cost);

        var shortfall = GetShortfall(colors, tokens, pairs);
        foreach (var (token, candidate, _) in candidates)
        {
            var current = colors[token];
            colors[token] = candidate;
            if (GetShortfall(colors, tokens, pairs) < shortfall - ShortfallTolerance)
            {
                return true;
            }
            colors[token] = current;
        }

        return false;
    }

    private void OrderShade(string family, int shade, int neighbour, bool darker, IDictionary<string, PaletteColor?> colors, ISet<string> tokens, IList<PaletteContrastPair> pairs)
    {
        var token = $"color_{family}_{shade}";
        if (FindColor(colors, token) is not { } current || FindColor(colors, $"color_{family}_{neighbour}") is not { } next)
        {
            return;
        }

        var (hue, saturation, lightness) = current.ToHsl();
        var nextLightness = next.ToHsl().Lightness;
        var target = darker ? Math.Min(lightness, nextLightness - ShadeOrderStep) : Math.Max(lightness, nextLightness + ShadeOrderStep);
        if (target == lightness)
        {
            return;
        }

        var shortfall = GetShortfall(colors, tokens, pairs);
        colors[token] = PaletteColor.FromHsl(hue, saturation, target);
        if (GetShortfall(colors, tokens, pairs) > shortfall)
        {
            colors[token] = current;
        }
    }

    private static PaletteColor? FindColor(IDictionary<string, PaletteColor?> colors, string token) =>
        colors.TryGetValue(token, out var color) ? color : null;

    // the white and the black of a palette: every surface and text falls back to them
    private static bool IsFixed(string token) => token is PaletteContrastPairs.AdditionalLight or PaletteContrastPairs.AdditionalDark;

    // the colors nearest to the given one that reach the ratio, by moving its lightness towards black and towards white
    private static IEnumerable<(PaletteColor Color, double LightnessChange)> FindContrastingColors(PaletteColor color, PaletteColor other, double minimumRatio)
    {
        var (hue, saturation, lightness) = color.ToHsl();

        foreach (var limit in new[] { 0d, 1d })
        {
            if (ColorContrast.GetContrastRatio(PaletteColor.FromHsl(hue, saturation, limit), other) < minimumRatio)
            {
                continue;
            }

            // the nearest lightness that passes, between the color's (failing) and the limit (passing)
            var failing = lightness;
            var passing = limit;
            for (var step = 0; step < SearchSteps; step++)
            {
                var middle = (failing + passing) / 2;
                if (ColorContrast.GetContrastRatio(PaletteColor.FromHsl(hue, saturation, middle), other) >= minimumRatio)
                {
                    passing = middle;
                }
                else
                {
                    failing = middle;
                }
            }

            yield return (PaletteColor.FromHsl(hue, saturation, passing), Math.Abs(passing - lightness));
        }
    }

    private PairEvaluation Evaluate(PaletteContrastPair pair, IDictionary<string, PaletteColor?> colors, ISet<string> tokens)
    {
        var textToken = ResolveToken(pair.Text, colors, tokens);
        var backgroundToken = ResolveToken(pair.Background, colors, tokens);
        if (textToken == null || backgroundToken == null || colors[textToken] is not { } text || colors[backgroundToken] is not { } background)
        {
            return null;
        }

        var ratio = ColorContrast.GetContrastRatio(text, background);
        return new PairEvaluation(textToken, text, backgroundToken, background, ratio, ratio >= pair.MinimumRatio);
    }

    // how far the pairs fall short of their ratios, in total
    private double GetShortfall(IDictionary<string, PaletteColor?> colors, ISet<string> tokens, IList<PaletteContrastPair> pairs) =>
        pairs.Select(x => Evaluate(x, colors, tokens)).Zip(pairs).Sum(x => x.First is { Passes: false } evaluation ? x.Second.MinimumRatio - evaluation.Ratio : 0);

    private static Dictionary<string, string> ToHex(Dictionary<string, PaletteColor?> colors) =>
        colors.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value.Value.ToHex());

    private sealed record PairEvaluation(string TextToken, PaletteColor TextColor, string BackgroundToken, PaletteColor BackgroundColor, double Ratio, bool Passes);
}
