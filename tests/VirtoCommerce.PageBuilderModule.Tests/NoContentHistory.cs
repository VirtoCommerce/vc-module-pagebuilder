using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.History;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class NoContentHistory : IContentHistory
{
    public Task<ContentHistoryStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ContentHistoryStatus.Unavailable);

    public Task RecordAsync(string storeId, ContentHistoryRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<ContentVersionPage> ListAsync(string storeId, IList<HistoryItem> items, string cursor, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ContentVersionPage { Status = ContentHistoryStatus.Unavailable });

    public Task<string> ReadAsync(string storeId, HistoryItem item, string versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string>(null);
}
