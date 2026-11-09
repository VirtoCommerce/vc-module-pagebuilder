namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerDiscardRequest
{
    /// <summary>The page, named as in a save; its content and ETag are not read.</summary>
    public DesignerSavePage Page { get; set; }

    /// <summary>The theme folder and the file path: "default/sections/header-group.json".</summary>
    public IList<string> ThemeFiles { get; set; } = [];
}
