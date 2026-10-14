namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// A page that already serves a permalink in a language.
/// </summary>
public class PermalinkUsage
{
    public string GroupId { get; set; }

    public string Name { get; set; }

    public string CultureName { get; set; }

    public string MultilanguagePageId { get; set; }
}
