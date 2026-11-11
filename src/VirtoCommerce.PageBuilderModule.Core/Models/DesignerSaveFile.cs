namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerSaveFile
{
    /// <summary>The theme folder and the file path: "default/sections/header-group.json".</summary>
    public string Path { get; set; }

    public string Content { get; set; }

    public string ETag { get; set; }
}
