namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>A palette color the storefront puts on another one, and the contrast they need.</summary>
public class PaletteContrastPair
{
    public string Name { get; set; }

    public PaletteTokenReference Text { get; set; }

    public PaletteTokenReference Background { get; set; }

    public double MinimumRatio { get; set; }
}
