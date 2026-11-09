namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemeFileVersion
{
    /// <summary>Path in the store's theme storage, e.g. "default/templates/product.json".</summary>
    public string Path { get; set; }

    public string ETag { get; set; }
}
