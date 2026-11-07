namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemePaletteGenerationRequest
{
    /// <summary>The main brand color, <c>#rrggbb</c> or <c>#rgb</c>.</summary>
    public string Primary { get; set; }

    /// <summary>The second brand color: the secondary and accent colors. Optional.</summary>
    public string Secondary { get; set; }

    public ThemePaletteTokens Tokens { get; set; }
}
