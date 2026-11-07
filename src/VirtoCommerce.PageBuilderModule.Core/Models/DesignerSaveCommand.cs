namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>What one designer save writes as drafts.</summary>
public class DesignerSaveCommand
{
    public string StoreId { get; set; }

    /// <summary>The designer save the history records the change as.</summary>
    public string SaveId { get; set; }

    /// <summary>A database page, its access already checked.</summary>
    public GroupedPageBuilderPage Group { get; set; }

    public string PageContent { get; set; }

    /// <summary>The version the database page was read at.</summary>
    public string PageETag { get; set; }

    /// <summary>A page or a blog article kept as a file.</summary>
    public PageBuilderContentFile FilePage { get; set; }

    /// <summary>The theme files, saved as they are.</summary>
    public IList<DesignerSaveFile> ThemeFiles { get; set; } = [];
}
