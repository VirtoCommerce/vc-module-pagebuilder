namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// Which pages of the store already serve a permalink in the given languages.
/// </summary>
public class PermalinkUsageRequest
{
    public string StoreId { get; set; }

    public string Permalink { get; set; }

    /// <summary>Languages the permalink will be served in; empty checks every language.</summary>
    public IList<string> CultureNames { get; set; } = [];

    /// <summary>The page being saved and, for a Multilanguage Page, its own language versions.</summary>
    public IList<string> ExcludeGroupIds { get; set; } = [];

    public string ExcludeMultilanguagePageId { get; set; }
}
