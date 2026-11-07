namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>A contrast pair below its minimum ratio.</summary>
public class PaletteContrastFailure
{
    /// <summary><c>light</c> or <c>dark</c>.</summary>
    public string Scheme { get; set; }

    public string Pair { get; set; }

    /// <summary>The token whose color the text has: the pair's own, or its fallback.</summary>
    public string Text { get; set; }

    public string Background { get; set; }

    public string TextColor { get; set; }

    public string BackgroundColor { get; set; }

    /// <summary>Rounded down to two decimals.</summary>
    public double Ratio { get; set; }

    public double MinimumRatio { get; set; }
}
