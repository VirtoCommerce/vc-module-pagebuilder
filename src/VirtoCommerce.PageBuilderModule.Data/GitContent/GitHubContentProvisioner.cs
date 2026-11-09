using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>Creates, takes over, archives and unarchives the repositories of the sites in the organization, over the GitHub API.</summary>
public class GitHubContentProvisioner(
    IGitHubApiClient gitHubApiClient,
    IGitHubConnectionService gitHubConnectionService,
    IGitContentRepository gitContentRepository,
    IGitContentPolicy gitContentPolicy,
    IGitContentOwnership gitContentOwnership,
    IOptions<GitContentOptions> options,
    ILogger<GitHubContentProvisioner> logger)
    : IGitContentProvisioner
{
    private const int BranchWaitAttempts = 10;

    private static readonly TimeSpan BranchWaitDelay = TimeSpan.FromSeconds(1);

    public virtual async Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default)
    {
        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);

        var status = AbstractTypeFactory<GitContentSiteStatus>.TryCreateInstance();
        status.Organization = connection.Organization;
        status.BaseBranch = connection.BaseBranch;
        if (!connection.IsEnabled || !connection.IsConfigured)
        {
            return status;
        }

        status.Configured = true;
        try
        {
            var storeRepository = await gitContentPolicy.GetConfiguredRepositoryAsync(storeId, cancellationToken);
            status.Repository = storeRepository?.Name;
            status.Enabled = await gitContentPolicy.IsEnabledForStoreAsync(storeId, cancellationToken);

            var repositoryInfo = status.Repository == null ? null : await gitHubApiClient.GetRepositoryAsync(status.Repository, cancellationToken);
            if (repositoryInfo != null && await gitContentPolicy.IsInOrganizationAsync(status.Repository, cancellationToken))
            {
                gitContentOwnership.EnsureOwnedBy(status.Repository, gitContentOwnership.GetStoreId(repositoryInfo.Description), storeId);
            }

            if (repositoryInfo == null && storeRepository?.Source == GitRepositorySource.Site)
            {
                // not created yet: check that the organization is reachable
                await EnsureOrganizationReachableAsync(connection.Organization, cancellationToken);
            }

            status.Url = repositoryInfo?.HtmlUrl;
            if (repositoryInfo != null)
            {
                status.LastCommitSha = await gitContentRepository.GetBranchHeadShaAsync(status.Repository, connection.BaseBranch, cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Git status of store {StoreId} failed.", storeId);
            status.Url = null;
            status.LastCommitSha = null;
            status.Error = ex.Message;
        }

        return status;
    }

    public virtual async Task CheckOrganizationAsync(CancellationToken cancellationToken = default)
    {
        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(connection.Organization))
        {
            await EnsureOrganizationReachableAsync(connection.Organization, cancellationToken);
        }
    }

    public virtual async Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);

        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
        var storeRepository = await gitContentPolicy.GetConfiguredRepositoryAsync(storeId, cancellationToken)
                              ?? throw new GitContentException("Git content is not configured on this platform.");
        var repository = storeRepository.Name;
        // only the module's own repository is created or changed
        var isModuleRepository = storeRepository.Source == GitRepositorySource.Site;

        var repositoryInfo = await gitHubApiClient.GetRepositoryAsync(repository, cancellationToken);
        if (repositoryInfo == null)
        {
            if (!isModuleRepository)
            {
                throw new GitContentException($"The GitHub repository \"{repository}\" does not exist, or the platform has no access to it.");
            }

            repositoryInfo = await CreateRepositoryAsync(repository, storeId, cancellationToken);
        }

        if (await gitContentPolicy.IsInOrganizationAsync(repository, cancellationToken))
        {
            gitContentOwnership.EnsureOwnedBy(repository, gitContentOwnership.GetStoreId(repositoryInfo.Description), storeId);
        }

        if (!isModuleRepository)
        {
            // a hand-assigned repository deploys from its own branches
            if (await gitContentRepository.GetBranchHeadShaAsync(repository, connection.BaseBranch, cancellationToken) == null)
            {
                throw new GitContentException($"The GitHub repository \"{repository}\" has no branch \"{connection.BaseBranch}\" to publish to.");
            }

            return;
        }

        await AdoptRepositoryAsync(repository, repositoryInfo, storeId, cancellationToken);
        await CreateBranchesAsync(repository, repositoryInfo.DefaultBranch ?? connection.BaseBranch, connection.BaseBranch, cancellationToken);
    }

    public virtual async Task ConnectAsync(string storeId, CancellationToken cancellationToken = default)
    {
        await EnsureRepositoryAsync(storeId, cancellationToken);
        await gitContentPolicy.SetEnabledForStoreAsync(storeId, true, cancellationToken);
    }

    public virtual Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default) =>
        gitContentPolicy.SetEnabledForStoreAsync(storeId, false, cancellationToken);

    public virtual async Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repository) || !await gitContentPolicy.IsInOrganizationAsync(repository, cancellationToken))
        {
            return;
        }

        var repositoryInfo = await gitHubApiClient.GetRepositoryAsync(repository, cancellationToken);
        if (repositoryInfo == null || repositoryInfo.Archived)
        {
            return;
        }

        if (!string.Equals(gitContentOwnership.GetStoreId(repositoryInfo.Description), storeId, StringComparison.Ordinal))
        {
            logger.LogInformation("Store {StoreId} was deleted; repository {Repository} was not created for it and is left as it is.", storeId, repository);
            return;
        }

        await gitHubApiClient.UpdateRepositoryAsync(repository, new JObject { ["archived"] = true }, cancellationToken);
        logger.LogInformation("Store {StoreId} was deleted; its repository {Repository} is archived.", storeId, repository);
    }

    /// <summary>Creates the store repository: private, with a first commit, without issues, projects and wiki.</summary>
    protected virtual async Task<GitHubRepositoryInfo> CreateRepositoryAsync(string repository, string storeId, CancellationToken cancellationToken)
    {
        var settings = new JObject
        {
            ["name"] = GitHubApiClient.ParseRepository(repository).Name,
            ["description"] = gitContentOwnership.GetDescription(storeId),
            ["private"] = true,
            // initial commit, so the default branch exists
            ["auto_init"] = true,
            ["has_issues"] = false,
            ["has_projects"] = false,
            ["has_wiki"] = false,
        };

        var repositoryInfo = await gitHubApiClient.CreateRepositoryAsync(repository, settings, cancellationToken);
        if (repositoryInfo == null)
        {
            // created concurrently
            return await gitHubApiClient.GetRepositoryAsync(repository, cancellationToken)
                   ?? throw new GitContentException($"GitHub reports \"{repository}\" exists but does not show it.");
        }

        logger.LogInformation("Created repository {Repository} for store {StoreId}.", repository, storeId);
        return repositoryInfo;
    }

    /// <summary>Marks an existing repository as the store's and brings it back from the archive.</summary>
    protected virtual async Task AdoptRepositoryAsync(string repository, GitHubRepositoryInfo repositoryInfo, string storeId, CancellationToken cancellationToken)
    {
        var changes = new JObject();
        if (gitContentOwnership.GetStoreId(repositoryInfo.Description) == null)
        {
            changes["description"] = gitContentOwnership.GetDescription(storeId);
        }

        if (repositoryInfo.Archived)
        {
            changes["archived"] = false;
        }

        if (changes.HasValues)
        {
            await gitHubApiClient.UpdateRepositoryAsync(repository, changes, cancellationToken);
            gitContentOwnership.Invalidate(repository);
        }
    }

    /// <summary>Creates the base branch from the default one, and the release branch from the base one.</summary>
    protected virtual async Task CreateBranchesAsync(string repository, string defaultBranch, string baseBranch, CancellationToken cancellationToken)
    {
        await WaitForBranchAsync(repository, defaultBranch, cancellationToken);

        if (!string.Equals(defaultBranch, baseBranch, StringComparison.Ordinal))
        {
            await gitContentRepository.CreateBranchAsync(repository, baseBranch, defaultBranch, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(options.Value.ReleaseBranch))
        {
            await gitContentRepository.CreateBranchAsync(repository, options.Value.ReleaseBranch, baseBranch, cancellationToken);
        }
    }

    /// <summary>Waits for the first commit of a new repository.</summary>
    protected virtual async Task WaitForBranchAsync(string repository, string branch, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < BranchWaitAttempts; attempt++)
        {
            if (attempt > 0)
            {
                await DelayBeforeRetryAsync(cancellationToken);
            }

            if (await gitContentRepository.GetBranchHeadShaAsync(repository, branch, cancellationToken) != null)
            {
                return;
            }
        }

        throw new GitContentException($"The repository \"{repository}\" has no branch \"{branch}\" yet. Try again in a moment.");
    }

    protected virtual Task DelayBeforeRetryAsync(CancellationToken cancellationToken) => Task.Delay(BranchWaitDelay, cancellationToken);

    protected virtual async Task EnsureOrganizationReachableAsync(string organization, CancellationToken cancellationToken)
    {
        if (!await gitHubApiClient.HasOrganizationAsync(organization, cancellationToken))
        {
            throw new GitContentException($"The GitHub organization \"{organization}\" was not found, or the platform has no access to it.");
        }
    }
}
