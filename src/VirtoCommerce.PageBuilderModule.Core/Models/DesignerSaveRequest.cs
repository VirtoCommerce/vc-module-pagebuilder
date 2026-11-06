namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>What one designer Save writes as drafts: the page and the theme files.</summary>
public class DesignerSaveRequest
{
    public DesignerSavePage Page { get; set; }

    public IList<DesignerSaveFile> ThemeFiles { get; set; } = [];
}
