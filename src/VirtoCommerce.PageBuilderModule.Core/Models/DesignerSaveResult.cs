using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerSaveResult : ContentSaveResult
{
    /// <summary>The new version of a database page.</summary>
    public string ETag { get; set; }

    /// <summary>The new version of each theme file saved.</summary>
    public IList<ThemeFileVersion> ThemeFiles { get; set; } = [];
}
