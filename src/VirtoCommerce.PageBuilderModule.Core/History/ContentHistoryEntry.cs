namespace VirtoCommerce.PageBuilderModule.Core.History;

public class ContentHistoryEntry
{
    public HistoryItem Item { get; set; }

    /// <summary>Content of the item as saved.</summary>
    public string Content { get; set; }
}
