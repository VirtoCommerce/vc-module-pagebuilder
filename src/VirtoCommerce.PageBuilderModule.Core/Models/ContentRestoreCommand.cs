using VirtoCommerce.PageBuilderModule.Core.History;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class ContentRestoreCommand
{
    public string StoreId { get; set; }

    public string VersionId { get; set; }

    public string SaveId { get; set; }

    /// <summary>The caller has already checked access to it.</summary>
    public GroupedPageBuilderPage Group { get; set; }

    public IDictionary<HistoryItem, string> Contents { get; set; } = new Dictionary<HistoryItem, string>();
}
