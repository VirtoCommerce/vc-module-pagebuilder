using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerSaveResult : ContentSaveResult
{
    public string ETag { get; set; }

    public IList<ThemeFileVersion> ThemeFiles { get; set; } = [];
}
