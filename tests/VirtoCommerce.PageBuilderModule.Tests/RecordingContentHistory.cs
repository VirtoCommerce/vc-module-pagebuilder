using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.History;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class RecordingContentHistory : IContentHistory
{
    public List<(string StoreId, ContentHistoryRecord Record)> Records { get; } = [];

    public Dictionary<(string VersionId, string Item), string> Versions { get; } = [];

    public List<string> Listed { get; } = [];

    public Task<ContentHistoryStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ContentHistoryStatus.Enabled);

    public Task RecordAsync(string storeId, ContentHistoryRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add((storeId, record));
        return Task.CompletedTask;
    }

    public Task<ContentVersionPage> ListAsync(string storeId, IList<HistoryItem> items, string cursor, int take, CancellationToken cancellationToken = default)
    {
        Listed.Add(storeId);
        return Task.FromResult(new ContentVersionPage { Status = ContentHistoryStatus.Enabled });
    }

    public Task<string> ReadAsync(string storeId, HistoryItem item, string versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Versions.GetValueOrDefault((versionId, item.ToString())));
}
