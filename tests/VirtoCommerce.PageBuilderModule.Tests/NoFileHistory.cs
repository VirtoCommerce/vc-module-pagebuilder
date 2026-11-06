using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>The history of a store that keeps none: nothing is recorded, nothing is there.</summary>
internal sealed class NoFileHistory : IThemeFileHistoryService, IGroupedPageHistoryService
{
    public Task RecordAsync(string storeId, string path, string content, string summary, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<GitFileHistoryResult> GetHistoryAsync(string storeId, string path, string after = null, int take = 20,
        CancellationToken cancellationToken = default) => Task.FromResult<GitFileHistoryResult>(null);

    public Task<string> ReadContentAsync(string storeId, string path, string sha, CancellationToken cancellationToken = default) =>
        Task.FromResult<string>(null);

    public Task<string> GetPublishedContentAsync(string storeId, string path, CancellationToken cancellationToken = default) =>
        Task.FromResult<string>(null);

    public Task<bool> RestoreAsync(string storeId, string path, string sha, string version, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task RecordAsync(GroupedPageBuilderPage group, string content, string summary, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RecordRestoreAsync(GroupedPageBuilderPage group, string content, string sha, string version, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<GitFileHistoryResult> GetHistoryAsync(GroupedPageBuilderPage group, string after = null, int take = 20,
        CancellationToken cancellationToken = default) => Task.FromResult<GitFileHistoryResult>(null);

    public Task<string> ReadContentAsync(GroupedPageBuilderPage group, string sha, CancellationToken cancellationToken = default) =>
        Task.FromResult<string>(null);

    public Task<string> GetPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default) =>
        Task.FromResult<string>(null);

    public Task<bool> DiscardDraftAsync(GroupedPageBuilderPage group, ClaimsPrincipal user, string saveId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
