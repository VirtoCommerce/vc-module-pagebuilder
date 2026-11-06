namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class DesignerSaveResult
{
    /// <summary>The save the history records the changes as.</summary>
    public string SaveId { get; set; }

    /// <summary>The new version of a database page.</summary>
    public string ETag { get; set; }
}
