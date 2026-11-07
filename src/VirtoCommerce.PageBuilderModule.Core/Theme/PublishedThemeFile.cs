namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class PublishedThemeFile
{
    /// <summary>Path in the store's theme storage, e.g. "default/templates/product.json".</summary>
    public string Path { get; set; }

    public string Content { get; set; }
}
