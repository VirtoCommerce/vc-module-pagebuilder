namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>A database page (<see cref="GroupId"/> and <see cref="ETag"/>) or a file page (<see cref="Path"/> and <see cref="Type"/>).</summary>
public class DesignerSavePage
{
    public string GroupId { get; set; }

    /// <summary>The version the page was read at.</summary>
    public string ETag { get; set; }

    public string Path { get; set; }

    /// <summary>"pages" or "blogs".</summary>
    public string Type { get; set; }

    /// <summary>The page JSON, saved as it is.</summary>
    public string Content { get; set; }
}
