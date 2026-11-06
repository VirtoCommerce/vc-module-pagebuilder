namespace VirtoCommerce.PageBuilderModule.Core.History;

/// <summary>One save as the history keeps it.</summary>
public class ContentVersion
{
    public string VersionId { get; set; }

    public DateTime? Date { get; set; }

    public ContentVersionAuthor Author { get; set; }

    public string Description { get; set; }

    public string SaveId { get; set; }

    /// <summary>The version holds what is live for every item it changed.</summary>
    public bool Published { get; set; }

    /// <summary>The requested items the version changed.</summary>
    public IList<string> Items { get; set; } = [];
}
