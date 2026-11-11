using System.Globalization;
using System.Text.RegularExpressions;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Palette;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemePaletteService : IThemePaletteService
{
    protected const string AdditionalLight = "color_additional_50";
    protected const string AdditionalDark = "color_additional_950";

    private const string BodyBackground = "color_body_bg";
    private const string NeutralFamily = "neutral";

    private const int MaxAdjustmentRounds = 8;
    private const int SearchSteps = 24;
    private const double ShadeOrderStep = 0.03;
    private const double BaseShadePenalty = 0.5;
    private const double ShortfallTolerance = 1e-9;
    private const double LightestLightness = 0.985;
    private const double DarkestLightness = 0.04;

    private const double DerivedSecondaryMaxSaturation = 0.18;
    private const double DerivedSecondaryLightness = 0.45;

    private const double DarkShade50Saturation = 0.4;
    private const double DarkShade50Lightness = 0.13;
    private const double DarkShade100Saturation = 0.45;
    private const double DarkShade100Lightness = 0.18;
    private const double DarkShade950Saturation = 0.5;
    private const double DarkShade950Lightness = 0.06;

    private static readonly int[] Shades = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    private static readonly Regex ShadeToken = new(@"^color_([a-z]+)_(\d+)$", RegexOptions.None, TimeSpan.FromSeconds(1));
    private static readonly Regex VcToken = new(@"^color_vc_([a-z]+_[a-z]+)_([a-z]+)$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly PaletteColor DarkSurface = new(0x0a, 0x09, 0x0b);
    private static readonly PaletteColor DarkText = new(0xec, 0xea, 0xef);
    private static readonly PaletteColor SolidDarkText = new(0x16, 0x17, 0x1b);

    // fractions of the way from the base color (500) to the lightest and the darkest lightness
    private static readonly Dictionary<int, double> LighterShades = new() { [400] = 0.3, [300] = 0.5, [200] = 0.7, [100] = 0.85, [50] = 0.97 };
    private static readonly Dictionary<int, double> DarkerShades = new() { [600] = 0.2, [700] = 0.38, [800] = 0.55, [900] = 0.72, [950] = 0.85 };

    private static readonly string[] DefaultColorFamilies = ["primary", "secondary", "accent", "neutral", "success", "warning", "danger", "info"];


    // the default preset's neutral and status colors
    private static readonly Dictionary<string, PaletteColor> DefaultFixedBaseColors = new()
    {
        [NeutralFamily] = new PaletteColor(0x73, 0x73, 0x73),
        ["success"] = new PaletteColor(0x3e, 0x84, 0x5b),
        ["warning"] = new PaletteColor(0xfc, 0x9e, 0x00),
        ["danger"] = new PaletteColor(0xde, 0x31, 0x31),
        ["info"] = new PaletteColor(0x2b, 0x7e, 0xa8),
    };

    // light text and icons on white take a darker shade than the fill, to reach AA
    private static readonly Dictionary<string, string> DefaultLightButtonRoles = new()
    {
        ["background_solid"] = "color_{0}_500",
        ["border_solid"] = "color_{0}_500",
        ["background_soft"] = "color_{0}_100",
        ["border_soft"] = "color_{0}_100",
        ["text_soft"] = "color_{0}_800",
        ["icon_soft"] = "color_{0}_800",
        ["background_outline"] = AdditionalLight,
        ["border_outline"] = "color_{0}_500",
        ["text_outline"] = "color_{0}_700",
        ["icon_outline"] = "color_{0}_700",
        ["background_surface"] = AdditionalLight,
        ["border_surface"] = AdditionalLight,
        ["text_surface"] = "color_{0}_700",
        ["icon_surface"] = "color_{0}_700",
        ["text_ghost"] = "color_{0}_700",
        ["icon_ghost"] = "color_{0}_700",
        ["background_tonal"] = "color_{0}_100",
        ["border_tonal"] = "color_{0}_500",
        ["text_tonal"] = "color_{0}_800",
        ["icon_tonal"] = "color_{0}_800",
    };

    // as the storefront's default dark preset
    private static readonly Dictionary<string, string> DefaultDarkButtonRoles = new(DefaultLightButtonRoles)
    {
        ["text_outline"] = "color_{0}_800",
        ["icon_outline"] = "color_{0}_800",
        ["text_surface"] = "color_{0}_800",
        ["icon_surface"] = "color_{0}_800",
        ["text_ghost"] = "color_{0}_800",
        ["icon_ghost"] = "color_{0}_800",
    };

    // light as the storefront's fallbacks, dark as its default dark preset
    private static readonly Dictionary<string, (string Light, string Dark)> DefaultNamedTokens = new()
    {
        ["color_body_bg"] = ("color_neutral_50", "color_neutral_50"),
        ["color_body_text"] = ("color_neutral_950", "color_neutral_950"),
        ["color_link"] = ("color_accent_600", "color_accent_800"),
        ["color_link_hover"] = ("color_accent_700", "color_accent_900"),
        ["color_price"] = ("color_neutral_900", "color_neutral_900"),
        ["color_shape_icon_bg"] = ("color_secondary_500", "color_secondary_500"),
        ["color_shape_icon"] = (AdditionalLight, AdditionalDark),
        ["color_empty_list_icon"] = ("color_primary_500", "color_primary_500"),
        ["color_mobile_search_bar_bg"] = (AdditionalLight, "color_neutral_50"),
        ["color_mobile_menu_bg"] = ("color_secondary_900", "color_secondary_950"),
        ["color_mobile_menu_text"] = (AdditionalLight, AdditionalDark),
        ["color_mobile_menu_link"] = ("color_secondary_200", "color_secondary_800"),
        ["color_mobile_menu_link_active"] = (AdditionalLight, AdditionalDark),
        ["color_mobile_menu_icon"] = ("color_secondary_600", "color_secondary_500"),
        ["color_mobile_menu_icon_active"] = (AdditionalLight, AdditionalDark),
        ["color_mobile_menu_navigation"] = ("color_primary_400", "color_primary_400"),
        ["color_mobile_menu_control"] = ("color_primary_500", "color_primary_500"),
        ["color_mobile_active_control"] = ("color_primary_500", "color_primary_500"),
        ["color_header_top_bg"] = ("color_secondary_900", "color_secondary_950"),
        ["color_header_top_text"] = (AdditionalLight, AdditionalDark),
        ["color_header_top_link"] = ("color_accent_300", "color_accent_300"),
        ["color_header_top_link_hover"] = (AdditionalLight, AdditionalDark),
        ["color_header_top_link_active"] = ("color_accent_300", "color_accent_300"),
        ["color_header_bottom_bg"] = (AdditionalLight, "color_neutral_50"),
        ["color_header_bottom_text"] = ("color_neutral_900", "color_neutral_900"),
        ["color_header_bottom_link"] = ("color_secondary_700", "color_neutral_700"),
        ["color_header_bottom_link_hover"] = ("color_neutral_900", "color_neutral_950"),
        ["color_header_bottom_link_active"] = ("color_neutral_900", "color_neutral_950"),
        ["color_footer_top_bg"] = ("color_secondary_900", "color_secondary_950"),
        ["color_footer_top_text"] = (AdditionalLight, AdditionalDark),
        ["color_footer_top_link"] = ("color_neutral_400", "color_neutral_600"),
        ["color_footer_top_link_hover"] = (AdditionalLight, AdditionalDark),
        ["color_footer_top_link_active"] = (AdditionalLight, AdditionalDark),
        ["color_footer_bottom_bg"] = ("color_secondary_950", AdditionalLight),
        ["color_footer_bottom_text"] = (AdditionalLight, AdditionalDark),
        ["color_footer_bottom_link"] = ("color_accent_300", "color_accent_300"),
        ["color_footer_bottom_link_hover"] = (AdditionalLight, AdditionalDark),
        ["color_footer_bottom_link_active"] = (AdditionalLight, AdditionalDark),
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

    protected virtual IList<PaletteContrastPair> GetContrastPairs() => [.. CreateContrastPairs()];

    protected virtual IList<string> GetColorFamilies() => [.. DefaultColorFamilies];

    protected virtual IDictionary<string, PaletteColor> GetFixedBaseColors() => new Dictionary<string, PaletteColor>(DefaultFixedBaseColors);

    protected virtual IReadOnlyDictionary<string, string> GetButtonRoleSources(string scheme) =>
        scheme == Light ? DefaultLightButtonRoles : DefaultDarkButtonRoles;

    protected virtual string GetNamedTokenSource(string token, string scheme) =>
        DefaultNamedTokens.TryGetValue(token, out var source) ? (scheme == Light ? source.Light : source.Dark) : null;

    protected virtual IDictionary<string, PaletteColor> GetBaseColors(ThemePaletteGenerationRequest request)
    {
        PaletteColor.TryParse(request.Primary, out var primary);
        var colors = GetFixedBaseColors();
        colors["primary"] = primary;

        if (PaletteColor.TryParse(request.Secondary, out var secondary))
        {
            colors["secondary"] = secondary;
            colors["accent"] = secondary;
        }
        else
        {
            var (hue, saturation, _) = primary.ToHsl();
            colors["secondary"] = PaletteColor.FromHsl(hue, Math.Min(saturation, DerivedSecondaryMaxSaturation), DerivedSecondaryLightness);
            colors["accent"] = primary;
        }

        return colors;
    }

    /// <summary>Dark shades invert unevenly, as the storefront's dark presets do.</summary>
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

        if (family == NeutralFamily)
        {
            return Shades.ToDictionary(x => x, x => light[1000 - x]);
        }

        return new Dictionary<int, PaletteColor>
        {
            [50] = PaletteColor.FromHsl(hue, saturation * DarkShade50Saturation, DarkShade50Lightness),
            [100] = PaletteColor.FromHsl(hue, saturation * DarkShade100Saturation, DarkShade100Lightness),
            [200] = light[200],
            [300] = light[300],
            [400] = light[400],
            [500] = light[500],
            [600] = light[600],
            [700] = light[300],
            [800] = light[200],
            [900] = light[100],
            [950] = PaletteColor.FromHsl(hue, saturation * DarkShade950Saturation, DarkShade950Lightness),
        };
    }

    /// <summary>Null for a token without a rule: it keeps the preset's color.</summary>
    protected virtual PaletteColor? CreateTokenColor(string token, string scheme, IDictionary<string, IDictionary<int, PaletteColor>> scales)
    {
        if (token == AdditionalLight)
        {
            return scheme == Light ? PaletteColor.White : DarkSurface;
        }

        if (token == AdditionalDark)
        {
            return scheme == Light ? PaletteColor.Black : DarkText;
        }

        var shadeMatch = ShadeToken.Match(token);
        if (shadeMatch.Success)
        {
            return scales.TryGetValue(shadeMatch.Groups[1].Value, out var scale)
                   && int.TryParse(shadeMatch.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var shade)
                   && scale.TryGetValue(shade, out var color)
                ? color
                : null;
        }

        var source = GetSourceToken(token, scheme);
        return source == null ? null : CreateTokenColor(source, scheme, scales);
    }

    protected virtual PaletteColor CreateSolidTextColor(PaletteColor background) =>
        ColorContrast.GetContrastRatio(PaletteColor.White, background) >= ColorContrast.GetContrastRatio(SolidDarkText, background)
            ? PaletteColor.White
            : SolidDarkText;

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

    protected virtual double GetAdjustmentCost(string token, double lightnessChange) =>
        token.EndsWith("_500", StringComparison.Ordinal) ? lightnessChange + BaseShadePenalty : lightnessChange;

    protected virtual void KeepShadeOrder(IDictionary<string, PaletteColor?> colors, ISet<string> tokens, IList<PaletteContrastPair> pairs)
    {
        foreach (var family in GetColorFamilies())
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

    protected virtual string GetSourceToken(string token, string scheme)
    {
        if (GetNamedTokenSource(token, scheme) is { } named)
        {
            return named;
        }

        var match = VcToken.Match(token);

        return match.Success && GetButtonRoleSources(scheme).TryGetValue(match.Groups[1].Value, out var source)
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
    private static bool IsFixed(string token) => token is AdditionalLight or AdditionalDark;

    private static IEnumerable<(PaletteColor Color, double LightnessChange)> FindContrastingColors(PaletteColor color, PaletteColor other, double minimumRatio)
    {
        var (hue, saturation, lightness) = color.ToHsl();

        foreach (var limit in new[] { 0d, 1d })
        {
            if (ColorContrast.GetContrastRatio(PaletteColor.FromHsl(hue, saturation, limit), other) < minimumRatio)
            {
                continue;
            }

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

    private double GetShortfall(IDictionary<string, PaletteColor?> colors, ISet<string> tokens, IList<PaletteContrastPair> pairs) =>
        pairs.Sum(x => Evaluate(x, colors, tokens) is { Passes: false } evaluation ? x.MinimumRatio - evaluation.Ratio : 0);

    private static Dictionary<string, string> ToHex(Dictionary<string, PaletteColor?> colors) =>
        colors.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value.Value.ToHex());

    private List<PaletteContrastPair> CreateContrastPairs()
    {
        var pairs = new List<PaletteContrastPair>
        {
            CreatePair("body-text", CreateTokenReference("body_text", "neutral_950"), CreateBodyReference(), TextContrastRatio),
            CreatePair("link", CreateTokenReference("link", "accent_600"), CreateBodyReference(), TextContrastRatio),
            CreatePair("link-hover", CreateTokenReference("link_hover", "accent_700"), CreateBodyReference(), TextContrastRatio),
            CreatePair("price", CreateTokenReference("price", "neutral_900"), CreateReference(AdditionalLight, null), TextContrastRatio),
            CreatePair("shape-icon", CreateTokenReference("shape_icon", "additional_50"), CreateTokenReference("shape_icon_bg", "secondary_500"), NonTextContrastRatio),
            CreatePair("empty-list-icon", CreateTokenReference("empty_list_icon", "primary_500"), CreateBodyReference(), NonTextContrastRatio),
        };

        pairs.AddRange(CreateAreaPairs("header_top", "secondary_900", "additional_50", "accent_300", "additional_50", "accent_300"));
        pairs.AddRange(CreateAreaPairs("header_bottom", "additional_50", "neutral_900", "secondary_700", "neutral_900", "neutral_900"));
        pairs.AddRange(CreateAreaPairs("footer_top", "secondary_900", "additional_50", "neutral_400", "additional_50", "additional_50"));
        pairs.AddRange(CreateAreaPairs("footer_bottom", "secondary_950", "additional_50", "accent_300", "additional_50", "additional_50"));

        var mobileMenu = CreateTokenReference("mobile_menu_bg", "secondary_900");
        pairs.Add(CreatePair("mobile-menu-text", CreateTokenReference("mobile_menu_text", "additional_50"), mobileMenu, TextContrastRatio));
        pairs.Add(CreatePair("mobile-menu-link", CreateTokenReference("mobile_menu_link", "secondary_200"), mobileMenu, TextContrastRatio));
        pairs.Add(CreatePair("mobile-menu-link-active", CreateTokenReference("mobile_menu_link_active", "additional_50"), mobileMenu, TextContrastRatio));
        pairs.Add(CreatePair("mobile-menu-navigation", CreateTokenReference("mobile_menu_navigation", "primary_400"), mobileMenu, TextContrastRatio));
        pairs.Add(CreatePair("mobile-menu-icon", CreateTokenReference("mobile_menu_icon", "secondary_600"), mobileMenu, NonTextContrastRatio));
        pairs.Add(CreatePair("mobile-menu-icon-active", CreateTokenReference("mobile_menu_icon_active", "additional_50"), mobileMenu, NonTextContrastRatio));
        pairs.Add(CreatePair("mobile-menu-control", CreateTokenReference("mobile_menu_control", "primary_500"), mobileMenu, NonTextContrastRatio));

        foreach (var family in GetColorFamilies())
        {
            pairs.AddRange(CreateButtonPairs(family));
        }

        return pairs;
    }

    private static IEnumerable<PaletteContrastPair> CreateAreaPairs(string area, string background, string text, string link, string linkHover, string linkActive)
    {
        var name = area.Replace('_', '-');
        var areaBackground = CreateTokenReference($"{area}_bg", background);

        yield return CreatePair($"{name}-text", CreateTokenReference($"{area}_text", text), areaBackground, TextContrastRatio);
        yield return CreatePair($"{name}-link", CreateTokenReference($"{area}_link", link), areaBackground, TextContrastRatio);
        yield return CreatePair($"{name}-link-hover", CreateTokenReference($"{area}_link_hover", linkHover), areaBackground, TextContrastRatio);
        yield return CreatePair($"{name}-link-active", CreateTokenReference($"{area}_link_active", linkActive), areaBackground, TextContrastRatio);
    }

    // the storefront's badges and chips use the same tokens as its buttons
    private static IEnumerable<PaletteContrastPair> CreateButtonPairs(string family)
    {
        var solidText = family == "warning" ? "warning_900" : "additional_50";
        var plainText = family switch
        {
            "secondary" => "secondary_600",
            "warning" => "warning_700",
            _ => $"{family}_500",
        };

        yield return CreatePair($"button-solid-{family}", CreateVcTokenReference("text_solid", family, solidText), CreateVcTokenReference("background_solid", family, $"{family}_500"), TextContrastRatio);
        yield return CreatePair($"button-soft-{family}", CreateVcTokenReference("text_soft", family, $"{family}_800"), CreateVcTokenReference("background_soft", family, $"{family}_100"), TextContrastRatio);
        yield return CreatePair($"button-outline-{family}", CreateVcTokenReference("text_outline", family, plainText), CreateVcTokenReference("background_outline", family, "additional_50"), TextContrastRatio);
        yield return CreatePair($"button-surface-{family}", CreateVcTokenReference("text_surface", family, plainText), CreateVcTokenReference("background_surface", family, "additional_50"), TextContrastRatio);
        yield return CreatePair($"button-ghost-{family}", CreateVcTokenReference("text_ghost", family, plainText), CreateBodyReference(), TextContrastRatio);
        yield return CreatePair($"button-tonal-{family}", CreateVcTokenReference("text_tonal", family, $"{family}_800"), CreateVcTokenReference("background_tonal", family, $"{family}_100"), TextContrastRatio);
    }

    private static PaletteContrastPair CreatePair(string name, PaletteTokenReference text, PaletteTokenReference background, double minimumRatio)
    {
        var pair = AbstractTypeFactory<PaletteContrastPair>.TryCreateInstance();
        pair.Name = name;
        pair.Text = text;
        pair.Background = background;
        pair.MinimumRatio = minimumRatio;
        return pair;
    }

    private static PaletteTokenReference CreateTokenReference(string name, string fallback) => CreateReference($"color_{name}", $"color_{fallback}");

    private static PaletteTokenReference CreateVcTokenReference(string role, string family, string fallback) => CreateTokenReference($"vc_{role}_{family}", fallback);

    private static PaletteTokenReference CreateBodyReference() => CreateReference(BodyBackground, "color_neutral_50");

    private static PaletteTokenReference CreateReference(string token, string fallback)
    {
        var reference = AbstractTypeFactory<PaletteTokenReference>.TryCreateInstance();
        reference.Token = token;
        reference.Fallback = fallback;
        return reference;
    }

    private sealed record PairEvaluation(string TextToken, PaletteColor TextColor, string BackgroundToken, PaletteColor BackgroundColor, double Ratio, bool Passes);
}
