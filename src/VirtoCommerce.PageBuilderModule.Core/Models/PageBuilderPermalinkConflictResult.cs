namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// The 409 response to publishing a page while other published pages have its permalink for the same visitors.
/// </summary>
public class PageBuilderPermalinkConflictResult
{
    public string Error { get; set; }

    public IList<PageBuilderPermalinkUsage> Conflicts { get; set; } = [];
}
