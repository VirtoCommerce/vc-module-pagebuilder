using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>A provisioner that does nothing but remember which stores asked for their repository or connected.</summary>
public sealed class FakeGitContentProvisioner : IGitContentProvisioner
{
    public List<string> Ensured { get; } = [];

    public Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GitContentSiteStatus { Configured = true, Repository = "o/r" });

    /// <summary>What asking GitHub about the organization fails with; <c>null</c> when it answers.</summary>
    public Exception OrganizationFailure { get; init; }

    public List<string> Connected { get; } = [];

    public Task CheckOrganizationAsync(CancellationToken cancellationToken = default) =>
        OrganizationFailure == null ? Task.CompletedTask : Task.FromException(OrganizationFailure);

    public Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default)
    {
        Ensured.Add(storeId);
        return Task.CompletedTask;
    }

    public Task ConnectAsync(string storeId, CancellationToken cancellationToken = default)
    {
        Connected.Add(storeId);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
