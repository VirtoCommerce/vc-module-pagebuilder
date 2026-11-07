namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>Writes theme drafts together with the rest of a save, all or nothing.</summary>
public interface IThemeDraftService
{
    /// <summary>
    /// Under the store's theme lock, writes the files as drafts and then runs <paramref name="saveOthersAsync"/>; when either fails,
    /// puts the drafts back as they were. The files are keyed by their path with the theme folder: "default/templates/product.json".
    /// </summary>
    Task<T> SaveDraftsAsync<T>(string storeId, IDictionary<string, string> files, Func<CancellationToken, Task<T>> saveOthersAsync,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Under the store's theme lock, deletes the drafts of the published files and then runs <paramref name="discardOthersAsync"/>;
    /// when it fails, puts the drafts back. A draft of a file never published is its only copy and stays. Returns the published
    /// files whose draft was deleted.
    /// </summary>
    Task<IList<PublishedThemeFile>> DiscardDraftsAsync(string storeId, IList<string> paths, Func<CancellationToken, Task> discardOthersAsync,
        CancellationToken cancellationToken = default);
}
