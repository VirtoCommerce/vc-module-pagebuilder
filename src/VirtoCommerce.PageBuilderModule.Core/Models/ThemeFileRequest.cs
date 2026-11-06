namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class ThemeFileRequest : PageBuilderStoreRequest
{
    /// <summary>Theme folder and file path, e.g. "default/templates/product.json".</summary>
    public string Path { get; set; }
}
