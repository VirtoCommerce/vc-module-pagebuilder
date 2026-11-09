namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>A theme file's version: what the designer's save sends back to show what it read.</summary>
public class ThemeFileVersion
{
    /// <summary>Path in the store's theme storage, e.g. "default/templates/product.json".</summary>
    public string Path { get; set; }

    public string ETag { get; set; }
}
