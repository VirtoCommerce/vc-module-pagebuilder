namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemePaletteCheckResult
{
    public IList<PaletteContrastFailure> Failures { get; set; } = [];

    /// <summary>Pairs not checked: a color they need is left to the storefront preset, or is not a color.</summary>
    public int Unchecked { get; set; }
}
