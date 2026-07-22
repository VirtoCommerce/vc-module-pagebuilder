namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IContentStreamRepository : IAsyncDisposable
{
    Task SaveBinaryAsync(string pageId, TextReader reader, CancellationToken cancellationToken = default);

    [Obsolete("Cannot distinguish missing content from empty content. Use TryLoadBinaryAsync instead.")]
    Task LoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the page content to <paramref name="writer"/> and reports whether the page actually has content.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the page row is missing or its content column is NULL — content was never written for
    /// this page, which is the state of a freshly created draft. <c>true</c> when content exists, including
    /// deliberately empty content: an empty string is a real value written by <see cref="SaveBinaryAsync"/>,
    /// NULL is not.
    /// </returns>
    /// <remarks>
    /// The default implementation is the compatibility path for external implementers: it delegates to
    /// <see cref="LoadBinaryAsync"/> and always reports <c>true</c>, because that method has already discarded
    /// the distinction. Implementations that read the content column directly override this with the real state.
    /// </remarks>
    async Task<bool> TryLoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default)
    {
#pragma warning disable CS0618 // compatibility path — the obsolete overload is the only interface member available here
        await LoadBinaryAsync(pageId, writer, cancellationToken);
#pragma warning restore CS0618
        return true;
    }
}
