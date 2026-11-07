namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class SharedComponentPlacementValidationResult
{
    /// <summary>The content places a shared component and the user may not read shared components.</summary>
    public bool IsForbidden { get; set; }

    /// <summary>Why the content or its shared component references are not valid for the store.</summary>
    public string Error { get; set; }

    public bool Succeeded => !IsForbidden && Error == null;
}
