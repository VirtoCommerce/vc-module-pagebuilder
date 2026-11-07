namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>The current publication of a store's theme and the drafts the next one puts live.</summary>
public class ThemeStatus
{
    /// <summary>Null before the first publication.</summary>
    public int? Publication { get; set; }

    /// <summary>Theme files with a saved draft, by their path in the theme, e.g. "templates/product.json".</summary>
    public IList<string> Drafts { get; set; } = [];
}
