namespace VirtoCommerce.PageBuilderModule.Core.History;

/// <summary>Version history of a store's database pages and theme files; a version is one save.</summary>
public interface IContentHistory
{
    Task<ContentHistoryStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Records every entry as one version. Best effort: a failure is logged, the save stands.</summary>
    Task RecordAsync(string storeId, ContentHistoryRecord record, CancellationToken cancellationToken = default);

    /// <summary>Gets the versions that changed any of the items, newest first.</summary>
    /// <exception cref="ArgumentException">The cursor is not one this history returned.</exception>
    Task<ContentVersionPage> ListAsync(string storeId, IList<HistoryItem> items, string cursor, int take, CancellationToken cancellationToken = default);

    /// <summary>Gets the item as of the version, changed by it or not; <c>null</c> when the item or the version does not exist.</summary>
    Task<string> ReadAsync(string storeId, HistoryItem item, string versionId, CancellationToken cancellationToken = default);
}
