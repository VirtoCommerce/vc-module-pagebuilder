namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>A theme file of a store, named as the theme storage names it: the theme's folder, then the file's path in it.</summary>
public class ThemeFileRequest : PageBuilderStoreRequest
{
    public string Path { get; set; }
}
