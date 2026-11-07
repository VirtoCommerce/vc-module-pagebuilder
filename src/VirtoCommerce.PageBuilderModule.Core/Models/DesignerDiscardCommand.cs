namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>What one designer discard throws away.</summary>
public class DesignerDiscardCommand
{
    public string StoreId { get; set; }

    /// <summary>The designer save the history records the change as.</summary>
    public string SaveId { get; set; }

    /// <summary>A database page, its access already checked.</summary>
    public GroupedPageBuilderPage Group { get; set; }

    /// <summary>A page or a blog article kept as a file; its content is not read.</summary>
    public PageBuilderContentFile FilePage { get; set; }

    /// <summary>The theme files by their path with the theme folder.</summary>
    public IList<string> ThemeFiles { get; set; } = [];
}
