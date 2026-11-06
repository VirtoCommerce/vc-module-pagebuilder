namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class ContentHistoryRequest : PageBuilderStoreRequest
{
    /// <summary><c>page:{groupId}</c> or <c>theme:{path}</c>.</summary>
    public IList<string> Items { get; set; } = [];

    public string Cursor { get; set; }

    public int? Take { get; set; }

    /// <summary>The version read or restored; such a request names at most one database page.</summary>
    public string VersionId { get; set; }
}
