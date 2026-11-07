using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Palette;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>
/// The colors the storefront puts on one another, with the fallbacks of its stylesheets
/// (docs/contracts/theme-snapshot.md, "Contrast pairs").
/// </summary>
public static class PaletteContrastPairs
{
    public const string AdditionalLight = "color_additional_50";
    public const string AdditionalDark = "color_additional_950";

    private const string BodyBackground = "color_body_bg";

    public static IReadOnlyList<string> Families { get; } = ["primary", "secondary", "accent", "neutral", "success", "warning", "danger", "info"];

    public static IReadOnlyList<PaletteContrastPair> All { get; } = CreatePairs();

    private static List<PaletteContrastPair> CreatePairs()
    {
        var pairs = new List<PaletteContrastPair>
        {
            Pair("body-text", Token("body_text", "neutral_950"), Body(), TextContrastRatio),
            Pair("link", Token("link", "accent_600"), Body(), TextContrastRatio),
            Pair("link-hover", Token("link_hover", "accent_700"), Body(), TextContrastRatio),
            Pair("price", Token("price", "neutral_900"), Reference(AdditionalLight, null), TextContrastRatio),
            Pair("shape-icon", Token("shape_icon", "additional_50"), Token("shape_icon_bg", "secondary_500"), NonTextContrastRatio),
            Pair("empty-list-icon", Token("empty_list_icon", "primary_500"), Body(), NonTextContrastRatio),
        };

        pairs.AddRange(Area("header_top", "secondary_900", "additional_50", "accent_300", "additional_50", "accent_300"));
        pairs.AddRange(Area("header_bottom", "additional_50", "neutral_900", "secondary_700", "neutral_900", "neutral_900"));
        pairs.AddRange(Area("footer_top", "secondary_900", "additional_50", "neutral_400", "additional_50", "additional_50"));
        pairs.AddRange(Area("footer_bottom", "secondary_950", "additional_50", "accent_300", "additional_50", "additional_50"));

        var mobileMenu = Token("mobile_menu_bg", "secondary_900");
        pairs.Add(Pair("mobile-menu-text", Token("mobile_menu_text", "additional_50"), mobileMenu, TextContrastRatio));
        pairs.Add(Pair("mobile-menu-link", Token("mobile_menu_link", "secondary_200"), mobileMenu, TextContrastRatio));
        pairs.Add(Pair("mobile-menu-link-active", Token("mobile_menu_link_active", "additional_50"), mobileMenu, TextContrastRatio));
        pairs.Add(Pair("mobile-menu-navigation", Token("mobile_menu_navigation", "primary_400"), mobileMenu, TextContrastRatio));
        pairs.Add(Pair("mobile-menu-icon", Token("mobile_menu_icon", "secondary_600"), mobileMenu, NonTextContrastRatio));
        pairs.Add(Pair("mobile-menu-icon-active", Token("mobile_menu_icon_active", "additional_50"), mobileMenu, NonTextContrastRatio));
        pairs.Add(Pair("mobile-menu-control", Token("mobile_menu_control", "primary_500"), mobileMenu, NonTextContrastRatio));

        foreach (var family in Families)
        {
            pairs.AddRange(Buttons(family));
        }

        return pairs;
    }

    // a header or footer row: its text and links on its background
    private static IEnumerable<PaletteContrastPair> Area(string area, string background, string text, string link, string linkHover, string linkActive)
    {
        var name = area.Replace('_', '-');
        var areaBackground = Token($"{area}_bg", background);

        yield return Pair($"{name}-text", Token($"{area}_text", text), areaBackground, TextContrastRatio);
        yield return Pair($"{name}-link", Token($"{area}_link", link), areaBackground, TextContrastRatio);
        yield return Pair($"{name}-link-hover", Token($"{area}_link_hover", linkHover), areaBackground, TextContrastRatio);
        yield return Pair($"{name}-link-active", Token($"{area}_link_active", linkActive), areaBackground, TextContrastRatio);
    }

    // VcButton's variants of a color; badges and chips use the same tokens
    private static IEnumerable<PaletteContrastPair> Buttons(string family)
    {
        var solidText = family == "warning" ? "warning_900" : "additional_50";
        var plainText = family switch
        {
            "secondary" => "secondary_600",
            "warning" => "warning_700",
            _ => $"{family}_500",
        };

        yield return Pair($"button-solid-{family}", Vc("text_solid", family, solidText), Vc("background_solid", family, $"{family}_500"), TextContrastRatio);
        yield return Pair($"button-soft-{family}", Vc("text_soft", family, $"{family}_800"), Vc("background_soft", family, $"{family}_100"), TextContrastRatio);
        yield return Pair($"button-outline-{family}", Vc("text_outline", family, plainText), Vc("background_outline", family, "additional_50"), TextContrastRatio);
        yield return Pair($"button-surface-{family}", Vc("text_surface", family, plainText), Vc("background_surface", family, "additional_50"), TextContrastRatio);
        yield return Pair($"button-ghost-{family}", Vc("text_ghost", family, plainText), Body(), TextContrastRatio);
        yield return Pair($"button-tonal-{family}", Vc("text_tonal", family, $"{family}_800"), Vc("background_tonal", family, $"{family}_100"), TextContrastRatio);
    }

    private static PaletteContrastPair Pair(string name, PaletteTokenReference text, PaletteTokenReference background, double minimumRatio) =>
        new() { Name = name, Text = text, Background = background, MinimumRatio = minimumRatio };

    private static PaletteTokenReference Token(string name, string fallback) => Reference($"color_{name}", $"color_{fallback}");

    private static PaletteTokenReference Vc(string role, string family, string fallback) => Token($"vc_{role}_{family}", fallback);

    private static PaletteTokenReference Body() => Reference(BodyBackground, "color_neutral_50");

    private static PaletteTokenReference Reference(string token, string fallback) => new() { Token = token, Fallback = fallback };
}
