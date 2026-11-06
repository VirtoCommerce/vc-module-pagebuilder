using System;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>The git flow on (or off) for every store, in one repository the stores came by as <paramref name="source"/> says.</summary>
internal sealed class FakeGitContentPolicy(bool enabled = true, GitRepositorySource source = GitRepositorySource.Site, string repository = "o/r")
    : IGitContentPolicy
{
    public Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(enabled);

    public Task SetEnabledForStoreAsync(string storeId, bool enabled, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<string> GetRepositoryAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(enabled ? repository : null);

    public Task<StoreRepository> GetRepositoryInfoAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(enabled ? new StoreRepository(repository, source) : null);

    public Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new StoreRepository(repository, source));

    public Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, string repositorySetting, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> IsInOrganizationAsync(string repository, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
