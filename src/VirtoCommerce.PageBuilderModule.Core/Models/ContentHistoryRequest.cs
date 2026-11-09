namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class ContentHistoryRequest : PageBuilderStoreRequest
{
    /// <summary><c>page:{groupId}</c> or <c>theme:{path}</c>.</summary>
    public IList<string> Items { get; set; } = [];

    public string Cursor { get; set; }

    public int? Take { get; set; }

    /// <summary>A request with a version names at most one database page.</summary>
    public string VersionId { get; set; }
}
