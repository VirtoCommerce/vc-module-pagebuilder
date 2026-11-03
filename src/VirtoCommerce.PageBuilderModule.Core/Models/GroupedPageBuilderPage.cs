using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class GroupedPageBuilderPage : AuditableEntity, IHasStoreId, ICloneable
{
    public string StoreId { get; set; }

    public IList<PageBuilderPage> Pages { get; set; } = [];

    public string CultureName { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }
    public string OrganizationId { get; set; }
    public bool Visibility { get; set; }
    public string UserGroups { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// The <see cref="MultilanguagePage"/> this page is a language version of; null for a Landing Page.
    /// Changed only through <c>IMultilanguagePageService</c> link/unlink, a regular save keeps the stored value.
    /// </summary>
    public string MultilanguagePageId { get; set; }

    /// <summary>
    /// Comma-separated inheritable fields this language version overrides (see <see cref="MultilanguagePageInheritance"/>).
    /// Null keeps the stored mask on save; an empty string clears it.
    /// </summary>
    public string OverriddenFields { get; set; }

    /// <summary>
    /// Source language version: when it was last published. Translation: the source publication it is based on;
    /// a translation is outdated when the source has been published after it. Null keeps the stored value on save.
    /// </summary>
    public DateTime? SourceRevisionDate { get; set; }

    public string Status
    {
        get
        {
            if (Pages?.All(x => x.Status == Archived) ?? false)
            {
                return Archived;
            }

            if (Pages?.Any(x => x.Status == Published) ?? false)
            {
                return Published;
            }

            return Draft;
        }
    }

    public bool HasChanges
    {
        get
        {
            return Pages?.Any(p => p.Status == Draft) ?? false;
        }
    }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
