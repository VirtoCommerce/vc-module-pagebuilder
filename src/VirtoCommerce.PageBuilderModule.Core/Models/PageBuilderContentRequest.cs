namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderContentRequest : IHasStoreId
{
    public string StoreId { get; set; }

    public string ContentType { get; set; }

    public string Theme { get; set; }

    public IList<string> Paths { get; set; } = [];

    /// <summary>Page documents of pages or blogs: the only content the git actions address.</summary>
    public IList<string> Pages { get; set; } = [];

    /// <summary>Draft blobs, named by their exact "-draft" path, that the request deletes.</summary>
    public IList<string> LegacyDrafts { get; set; } = [];

    /// <summary><c>null</c> unless the request writes: that is what tells a save from a read.</summary>
    public IList<PageBuilderContentFile> Files { get; set; }

    /// <summary>A commit sha; <c>null</c> when the request names no version.</summary>
    public string Ref { get; set; }

    public string Keyword { get; set; }

    public int? HistoryTake { get; set; }

    /// <summary>The cursor a previous page of history returned.</summary>
    public string HistoryAfter { get; set; }

    public string SchemaKind { get; set; }

    public string SchemaKey { get; set; }
}
