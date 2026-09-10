namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IContentStreamRepository : IAsyncDisposable
{
    /// <summary>
    /// Writes the page content to <paramref name="writer"/> and reports whether the page actually has content.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the page row is missing or its content column is NULL — content was never written for
    /// this page, which is the state of a freshly created draft. <c>true</c> when content exists, including
    /// deliberately empty content: an empty string is a real value written by <see cref="SavePageContentAsync"/>,
    /// NULL is not.
    /// </returns>
    Task<bool> TryLoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default);

    Task SavePageContentAsync(
        string pageId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies one page's content and its derived indexes onto another. The target ends up with exactly what the
    /// source had, including nothing at all when the source was never seeded.
    /// </summary>
    Task CopyPageContentAsync(
        string sourcePageId,
        string targetPageId,
        CancellationToken cancellationToken = default);
}
