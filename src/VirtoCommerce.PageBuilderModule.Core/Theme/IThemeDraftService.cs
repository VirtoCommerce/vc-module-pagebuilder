namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>Writes theme drafts together with the rest of a save, all or nothing.</summary>
public interface IThemeDraftService
{
    /// <summary>Reads the file as the designer edits it: its draft, else what is live. The path has the theme folder.</summary>
    Task<ThemeFileContent> ReadAsync(string storeId, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Under the store's theme lock, writes the files as drafts and then runs <paramref name="saveOthersAsync"/>; when either fails,
    /// puts the drafts back as they were. The files are keyed by their path with the theme folder: "default/templates/product.json".
    /// </summary>
    /// <param name="eTags">The version each file was read at, by its path; null writes the files whatever they hold.</param>
    /// <exception cref="Models.PageBuilderContentConflictException">A file was saved by someone else since it was read: nothing is written.</exception>
    Task<T> SaveDraftsAsync<T>(string storeId, IDictionary<string, string> files, IDictionary<string, string> eTags,
        Func<CancellationToken, Task<T>> saveOthersAsync, CancellationToken cancellationToken = default);

    /// <summary>
    /// Under the store's theme lock, deletes the drafts of the published files and then runs <paramref name="discardOthersAsync"/>;
    /// when it fails, puts the drafts back. Returns each file with what became of its draft.
    /// </summary>
    Task<IList<DiscardedThemeDraft>> DiscardDraftsAsync(string storeId, IList<string> paths, Func<CancellationToken, Task> discardOthersAsync,
        CancellationToken cancellationToken = default);
}
