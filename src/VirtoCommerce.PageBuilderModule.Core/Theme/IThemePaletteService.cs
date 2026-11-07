namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>Theme palettes that meet WCAG 2.2 AA contrast.</summary>
public interface IThemePaletteService
{
    /// <summary>Generates the light and dark palettes over the announced tokens from one or two brand colors.</summary>
    ThemePaletteGenerationResult GeneratePalette(ThemePaletteGenerationRequest request);

    /// <summary>Gets the contrast pairs a palette fails.</summary>
    ThemePaletteCheckResult CheckPalette(ThemePaletteCheckRequest request);
}
