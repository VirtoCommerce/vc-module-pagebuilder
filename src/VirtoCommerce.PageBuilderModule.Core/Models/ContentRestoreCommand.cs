using VirtoCommerce.PageBuilderModule.Core.History;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>The items of a version to write as their drafts.</summary>
public class ContentRestoreCommand
{
    public string StoreId { get; set; }

    public string VersionId { get; set; }

    /// <summary>The designer save the history records the change as.</summary>
    public string SaveId { get; set; }

    /// <summary>The database page of the restored page item, its access already checked.</summary>
    public GroupedPageBuilderPage Group { get; set; }

    /// <summary>The content of each item as of the version.</summary>
    public IDictionary<HistoryItem, string> Contents { get; set; } = new Dictionary<HistoryItem, string>();
}
