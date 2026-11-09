namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class PaletteContrastPair
{
    public string Name { get; set; }

    public PaletteTokenReference Text { get; set; }

    public PaletteTokenReference Background { get; set; }

    public double MinimumRatio { get; set; }
}
