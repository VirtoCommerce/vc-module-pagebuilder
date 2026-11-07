using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class FakeGitContentPolicy(bool enabled = true, GitRepositorySource source = GitRepositorySource.Site, string repositoryFormat = "o/r")
    : IGitContentPolicy
{
    public Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(enabled);

    public Task SetEnabledForStoreAsync(string storeId, bool enabled, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<string> GetRepositoryAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(enabled ? GetRepositoryName(storeId) : null);

    public Task<StoreRepository> GetRepositoryInfoAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(enabled ? CreateStoreRepository(storeId) : null);

    public Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateStoreRepository(storeId));

    public Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, string repositorySetting, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> IsInOrganizationAsync(string repository, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    private string GetRepositoryName(string storeId) => string.Format(CultureInfo.InvariantCulture, repositoryFormat, storeId);

    private StoreRepository CreateStoreRepository(string storeId) => new(GetRepositoryName(storeId), source);
}
