namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>WCAG 2.2 relative luminance and contrast ratio.</summary>
public static class ColorContrast
{
    public static double GetRelativeLuminance(PaletteColor color) =>
        (0.2126 * Linearize(color.R)) + (0.7152 * Linearize(color.G)) + (0.0722 * Linearize(color.B));

    public static double GetContrastRatio(PaletteColor first, PaletteColor second)
    {
        var firstLuminance = GetRelativeLuminance(first);
        var secondLuminance = GetRelativeLuminance(second);

        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double Linearize(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
