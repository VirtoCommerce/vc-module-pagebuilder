namespace VirtoCommerce.PageBuilderModule.Core.History;

public class ContentHistoryEntry
{
    public HistoryItem Item { get; set; }

    /// <summary>The exact text saved: the published version is told by it.</summary>
    public string Content { get; set; }
}
