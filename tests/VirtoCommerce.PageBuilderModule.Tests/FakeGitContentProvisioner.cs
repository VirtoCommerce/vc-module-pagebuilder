using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class FakeGitContentProvisioner : IGitContentProvisioner
{
    public List<string> Ensured { get; } = [];

    public List<string> Connected { get; } = [];

    public Exception OrganizationFailure { get; init; }

    public Exception ConnectFailure { get; init; }

    public Action OnEnsure { get; init; }

    public Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GitContentSiteStatus { Configured = true, Repository = "o/r" });

    public Task CheckOrganizationAsync(CancellationToken cancellationToken = default) =>
        OrganizationFailure == null ? Task.CompletedTask : Task.FromException(OrganizationFailure);

    public Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default)
    {
        Ensured.Add(storeId);
        OnEnsure?.Invoke();
        return Task.CompletedTask;
    }

    public Task ConnectAsync(string storeId, CancellationToken cancellationToken = default)
    {
        if (ConnectFailure != null)
        {
            throw ConnectFailure;
        }

        Connected.Add(storeId);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
