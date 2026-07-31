using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal class NoopLinkedComponentReferenceIndexService : IPageBuilderLinkedComponentReferenceIndexService
{
    public virtual Task ValidateReferencesForStoreAsync(
        string storeId,
        IEnumerable<string> contents,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public virtual Task RebuildPageIndexAsync(
        string pageId,
        string content,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public virtual Task<IList<string>> GetPageIdsAsync(
        IEnumerable<string> linkedComponentIds,
        CancellationToken cancellationToken = default) => Task.FromResult<IList<string>>([]);
}
