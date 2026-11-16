namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderContentRequest : IHasStoreId
{
    public string StoreId { get; set; }

    public string ContentType { get; set; }

    public string Theme { get; set; }

    public IList<string> Paths { get; set; } = [];

    /// <summary>Kept apart from <see cref="Paths"/>: the publish actions address nothing but pages and blog articles.</summary>
    public IList<string> Pages { get; set; } = [];

    /// <summary><c>null</c> unless the request writes: that is what tells a save from a read.</summary>
    public IList<PageBuilderContentFile> Files { get; set; }

    public string Keyword { get; set; }
}
