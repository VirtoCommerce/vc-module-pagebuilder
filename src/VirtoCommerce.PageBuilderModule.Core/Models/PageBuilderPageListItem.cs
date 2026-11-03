namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// A row of the pages list: a Landing Page, or a Multilanguage Page with all its languages.
/// </summary>
public class PageBuilderPageListItem
{
    /// <summary><see cref="ModuleConstants.PageTypes"/></summary>
    public string Type { get; set; }

    /// <summary>Grouped page id for a Landing Page, multilanguage page id for a Multilanguage Page.</summary>
    public string Id { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    /// <summary>Set for a Landing Page.</summary>
    public GroupedPageBuilderPage Page { get; set; }

    /// <summary>Set for a Multilanguage Page.</summary>
    public MultilanguagePage MultilanguagePage { get; set; }

    /// <summary>Language pills of a Multilanguage Page.</summary>
    public IList<MultilanguagePageLanguage> Languages { get; set; }
}
