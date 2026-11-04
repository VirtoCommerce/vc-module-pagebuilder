namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderContentRequest : IHasStoreId
{
    public string StoreId { get; set; }

    /// <summary>themes, pages or blogs. A save leaves it empty: each of its files names its own.</summary>
    public string ContentType { get; set; }

    /// <summary>Optional: without one the store's default theme is used.</summary>
    public string Theme { get; set; }

    /// <summary>The paths the request reads or names.</summary>
    public IList<string> Paths { get; set; } = [];

    /// <summary>The files the request writes; <c>null</c> when it writes none.</summary>
    public IList<PageBuilderContentFile> Files { get; set; }
}
