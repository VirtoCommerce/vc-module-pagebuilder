namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemePaletteGenerationRequest
{
    /// <summary><c>#rrggbb</c> or <c>#rgb</c>.</summary>
    public string Primary { get; set; }

    public string Secondary { get; set; }

    public ThemePaletteTokens Tokens { get; set; }
}
