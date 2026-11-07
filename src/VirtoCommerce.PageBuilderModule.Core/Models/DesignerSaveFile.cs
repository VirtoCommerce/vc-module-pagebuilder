namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerSaveFile
{
    /// <summary>The theme folder and the file path: "default/sections/header-group.json".</summary>
    public string Path { get; set; }

    /// <summary>The file JSON, saved as it is.</summary>
    public string Content { get; set; }
}
