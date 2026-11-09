namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerSaveRequest
{
    public DesignerSavePage Page { get; set; }

    public IList<DesignerSaveFile> ThemeFiles { get; set; } = [];
}
