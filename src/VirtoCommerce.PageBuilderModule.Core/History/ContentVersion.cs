namespace VirtoCommerce.PageBuilderModule.Core.History;

public class ContentVersion
{
    public string VersionId { get; set; }

    public DateTime? Date { get; set; }

    public ContentVersionAuthor Author { get; set; }

    public string Description { get; set; }

    public string SaveId { get; set; }

    /// <summary>The version holds what is live for every item it changed.</summary>
    public bool Published { get; set; }

    public IList<string> Items { get; set; } = [];
}
