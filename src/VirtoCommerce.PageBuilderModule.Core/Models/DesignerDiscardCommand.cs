namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerDiscardCommand
{
    public string StoreId { get; set; }

    public string SaveId { get; set; }

    /// <summary>The caller has already checked access to it.</summary>
    public GroupedPageBuilderPage Group { get; set; }

    public PageBuilderContentFile FilePage { get; set; }

    /// <summary>The theme files by their path with the theme folder.</summary>
    public IList<string> ThemeFiles { get; set; } = [];
}
