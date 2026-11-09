namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemePaletteGenerationResult
{
    public IDictionary<string, string> Light { get; set; }

    public IDictionary<string, string> Dark { get; set; }

    public IList<PaletteContrastFailure> Failures { get; set; } = [];
}
