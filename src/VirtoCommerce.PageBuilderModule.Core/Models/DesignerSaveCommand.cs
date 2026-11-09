namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerSaveCommand
{
    public string StoreId { get; set; }

    public string SaveId { get; set; }

    /// <summary>The caller has already checked access to it.</summary>
    public GroupedPageBuilderPage Group { get; set; }

    public string PageContent { get; set; }

    public string PageETag { get; set; }

    public PageBuilderContentFile FilePage { get; set; }

    public IList<DesignerSaveFile> ThemeFiles { get; set; } = [];
}
