using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// Root of a page that has a separate <see cref="GroupedPageBuilderPage"/> per store language.
/// Holds the shared name and the default settings that language versions inherit unless they override them
/// (see <see cref="MultilanguagePageInheritance"/>).
/// </summary>
public class MultilanguagePage : AuditableEntity, IHasStoreId, ICloneable
{
    public string StoreId { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }

    /// <summary>
    /// Source language: other language versions are translations of it. Initialized from the store default language.
    /// </summary>
    public string DefaultCultureName { get; set; }

    public string OrganizationId { get; set; }
    public bool Visibility { get; set; }
    public string UserGroups { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
