namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderContentRequest : IHasStoreId
{
    public string StoreId { get; set; }

    /// <summary>Empty for a save: each file names its own.</summary>
    public string ContentType { get; set; }

    /// <summary>Empty means the store's default theme.</summary>
    public string Theme { get; set; }

    public IList<string> Paths { get; set; } = [];

    /// <summary><c>null</c> unless the request writes: that is what tells a save from a read.</summary>
    public IList<PageBuilderContentFile> Files { get; set; }
}
