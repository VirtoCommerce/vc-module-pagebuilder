namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerSaveResult : ContentSaveResult
{
    /// <summary>The new version of a database page.</summary>
    public string ETag { get; set; }
}
