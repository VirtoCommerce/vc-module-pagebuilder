namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class SharedComponentPlacementValidationResult
{
    public bool IsForbidden { get; set; }

    public string Error { get; set; }

    public bool Succeeded => !IsForbidden && Error == null;
}
