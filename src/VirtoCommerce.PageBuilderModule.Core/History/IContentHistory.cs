namespace VirtoCommerce.PageBuilderModule.Core.History;

public interface IContentHistory
{
    Task<ContentHistoryStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Best effort: a failure is logged, the save stands.</summary>
    Task RecordAsync(string storeId, ContentHistoryRecord record, CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentException">The cursor is not one this history returned.</exception>
    Task<ContentVersionPage> ListAsync(string storeId, IList<HistoryItem> items, string cursor, int take, CancellationToken cancellationToken = default);

    /// <summary>Gets the item as of the version, changed by it or not; <c>null</c> when the item or the version does not exist.</summary>
    Task<string> ReadAsync(string storeId, HistoryItem item, string versionId, CancellationToken cancellationToken = default);
}
