namespace VirtoCommerce.PageBuilderModule.Core.History;

/// <summary>One save to record: what it changed, by whom and why.</summary>
public class ContentHistoryRecord
{
    /// <summary>"Saved", "Published", "Restored …", "Discarded unpublished changes".</summary>
    public string Description { get; set; }

    /// <summary>The designer save the record belongs to.</summary>
    public string SaveId { get; set; }

    public string UserName { get; set; }

    public string UserEmail { get; set; }

    public IList<ContentHistoryEntry> Entries { get; set; } = [];
}
