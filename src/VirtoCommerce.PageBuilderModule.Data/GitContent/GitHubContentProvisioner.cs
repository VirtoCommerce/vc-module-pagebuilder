using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>
/// IGitContentProvisioner over the GitHub REST API:
///   GET   /repos/{repo}      — does it exist, is it archived, whose is it
///   POST  /orgs/{org}/repos  — create a site's repository
///   PATCH /repos/{repo}      — archive / unarchive it
///   GET   /orgs/{org}        — is the organization there at all (the connection test)
/// <para>
/// A repository's description says which store it was created for. That is how the module tells its
/// own repositories from anything else in the organization: it adopts or archives a repository only
/// when the description names the same store.
/// </para>
/// </summary>
public class GitHubContentProvisioner : IGitContentProvisioner
{
    private const int BranchWaitAttempts = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGitContentRepository _repository;
    private readonly IGitContentPolicy _policy;
    private readonly IGitContentOwnership _ownership;
    private readonly ISettingsManager _settingsManager;
    private readonly GitContentOptions _options;
    private readonly ILogger<GitHubContentProvisioner> _logger;

    public GitHubContentProvisioner(IHttpClientFactory httpClientFactory, IGitContentRepository repository, IGitContentPolicy policy,
        IGitContentOwnership ownership, ISettingsManager settingsManager, IOptions<GitContentOptions> options, ILogger<GitHubContentProvisioner> logger)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
        _policy = policy;
        _ownership = ownership;
        _settingsManager = settingsManager;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>How long to wait between looks for the first commit of a repository GitHub has just created.</summary>
    internal TimeSpan BranchWait { get; set; } = TimeSpan.FromSeconds(1);

    public virtual async Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.IsConfigured)
        {
            return new GitContentSiteStatus { Organization = _options.Organization, BaseBranch = _options.BaseBranch };
        }

        string repository = null;
        var enabled = false;
        try
        {
            var site = await _policy.ResolveRepositoryAsync(storeId, cancellationToken);
            repository = site?.Name;
            enabled = await _policy.IsEnabledForStoreAsync(storeId, cancellationToken);

            var found = await GetRepositoryInfoAsync(repository, cancellationToken);
            if (found != null && _options.IsInOrganization(repository))
            {
                // what every git action of the store would answer
                GitHubContentOwnership.EnsureOwnedBy(repository, GitHubContentOwnership.StoreOf(found), storeId);
            }
            if (found == null && site?.Source == GitRepositorySource.Site)
            {
                // the repository is simply not created yet; this proves the organization and the
                // App's installation on it are there to create it in
                await EnsureOrganizationReachableAsync(cancellationToken);
            }

            return new GitContentSiteStatus
            {
                Configured = true,
                Organization = _options.Organization,
                Enabled = enabled,
                Repository = repository,
                BaseBranch = _options.BaseBranch,
                Url = found?["html_url"]?.Value<string>(),
                LastCommitSha = found == null ? null : await _repository.GetBranchHeadShaAsync(repository, _options.BaseBranch, cancellationToken),
            };
        }
        // the status is also the connection test: whatever keeps GitHub from answering (an error, a timeout) is its
        // answer, unless the caller gave up
        catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
        {
            return new GitContentSiteStatus
            {
                Configured = true,
                Organization = _options.Organization,
                Enabled = enabled,
                Repository = repository,
                BaseBranch = _options.BaseBranch,
                Error = failure.Message,
            };
        }
    }

    public virtual async Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);

        var site = await _policy.ResolveRepositoryAsync(storeId, cancellationToken)
                   ?? throw new InvalidOperationException("Git content is not configured on this platform.");
        var repository = site.Name;
        // only the store's own repository in the organization is the module's to create, mark and bring back: one
        // assigned by hand is used as it is
        var own = site.Source == GitRepositorySource.Site;

        var found = await GetRepositoryInfoAsync(repository, cancellationToken);
        if (found == null)
        {
            if (!own)
            {
                throw new InvalidOperationException($"The GitHub repository \"{repository}\" does not exist, or the platform has no access to it.");
            }

            found = await CreateRepositoryAsync(repository, storeId, cancellationToken);
        }

        if (_options.IsInOrganization(repository))
        {
            GitHubContentOwnership.EnsureOwnedBy(repository, GitHubContentOwnership.StoreOf(found), storeId);
        }

        if (!own)
        {
            // its deployment follows its own branches: a base branch cut here would take publications where none is deployed
            if (await _repository.GetBranchHeadShaAsync(repository, _options.BaseBranch, cancellationToken) == null)
            {
                throw new InvalidOperationException($"The GitHub repository \"{repository}\" has no branch \"{_options.BaseBranch}\" to publish to.");
            }

            return;
        }

        var patch = new JObject();
        if (GitHubContentOwnership.StoreOf(found) == null)
        {
            // adopted: marked as the store's, so that no other store whose id names it works in it too
            patch["description"] = GitHubContentOwnership.Marker + storeId;
        }
        if (found["archived"]?.Value<bool>() == true)
        {
            // the store was deleted and has come back under the same id: its history is all there
            patch["archived"] = false;
        }
        if (patch.HasValues)
        {
            await PatchRepositoryAsync(repository, patch, cancellationToken);
            _ownership.Invalidate(repository);
        }

        var defaultBranch = found["default_branch"]?.Value<string>() ?? _options.BaseBranch;
        await WaitForBranchAsync(repository, defaultBranch, cancellationToken);

        if (!string.Equals(defaultBranch, _options.BaseBranch, StringComparison.Ordinal))
        {
            await _repository.CreateBranchAsync(repository, _options.BaseBranch, defaultBranch, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_options.ReleaseBranch))
        {
            await _repository.CreateBranchAsync(repository, _options.ReleaseBranch, _options.BaseBranch, cancellationToken);
        }
    }

    public virtual async Task ConnectAsync(string storeId, CancellationToken cancellationToken = default)
    {
        await EnsureRepositoryAsync(storeId, cancellationToken);
        await SetEnabledAsync(storeId, true);
    }

    public virtual Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default) =>
        SetEnabledAsync(storeId, false);

    public virtual async Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repository) || !_options.IsInOrganization(repository))
        {
            return;
        }

        var found = await GetRepositoryInfoAsync(repository, cancellationToken);
        if (found == null || found["archived"]?.Value<bool>() == true)
        {
            return;
        }

        if (!string.Equals(GitHubContentOwnership.StoreOf(found), storeId, StringComparison.Ordinal))
        {
            _logger.LogInformation("Store {StoreId} was deleted; repository {Repository} was not created for it and is left as it is.", storeId, repository);
            return;
        }

        await PatchRepositoryAsync(repository, new JObject { ["archived"] = true }, cancellationToken);
        _logger.LogInformation("Store {StoreId} was deleted; its repository {Repository} is archived.", storeId, repository);
    }

    /// <summary>
    /// Writes the switch straight to the store's settings rather than saving the store: a whole-store save
    /// could overwrite an administrator's concurrent edit of the store, and would announce a store change
    /// that did not happen. The store's cached copy is dropped so the change is seen at once.
    /// </summary>
    private async Task SetEnabledAsync(string storeId, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);

        await _settingsManager.SaveObjectSettingsAsync(
        [
            new ObjectSettingEntry(ModuleConstants.Settings.StoreLevelSettings.GitContentEnabled)
            {
                ObjectType = ModuleConstants.Settings.StoreSettingsObjectType,
                ObjectId = storeId,
                Value = enabled,
            },
        ]);

        GenericCachingRegion<Store>.ExpireTokenForKey(storeId);
        GenericSearchCachingRegion<Store>.ExpireRegion();
    }

    private async Task<JObject> CreateRepositoryAsync(string repository, string storeId, CancellationToken cancellationToken)
    {
        var separator = repository.IndexOf('/');
        var body = new JObject
        {
            ["name"] = repository[(separator + 1)..],
            ["description"] = GitHubContentOwnership.Marker + storeId,
            ["private"] = true,
            // a first commit, so there is a branch to cut every other one from
            ["auto_init"] = true,
            ["has_issues"] = false,
            ["has_projects"] = false,
            ["has_wiki"] = false,
        };

        var client = _httpClientFactory.CreateClient(GitHubContentRepository.HttpClientName);
        using var response = await client.PostAsync($"orgs/{repository[..separator]}/repos", JsonContent(body), cancellationToken);
        var details = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Created repository {Repository} for store {StoreId}.", repository, storeId);
            return JObject.Parse(details);
        }

        // created a moment ago by a request racing with this one — the same store's first save and its
        // "turn on" — which is the repository this one wanted
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity && details.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return await GetRepositoryInfoAsync(repository, cancellationToken)
                   ?? throw new InvalidOperationException($"GitHub reports \"{repository}\" exists but does not show it.");
        }

        throw Failed(response, details, $"create repository \"{repository}\"");
    }

    /// <summary>
    /// GitHub answers the create before the repository's first commit is readable; cutting a branch from
    /// it in that moment fails. Waits for it, then gives up with an answer that says to try again.
    /// </summary>
    private async Task WaitForBranchAsync(string repository, string branch, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < BranchWaitAttempts; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(BranchWait, cancellationToken);
            }

            if (await _repository.GetBranchHeadShaAsync(repository, branch, cancellationToken) != null)
            {
                return;
            }
        }

        throw new InvalidOperationException($"The repository \"{repository}\" has no branch \"{branch}\" yet. Try again in a moment.");
    }

    private async Task<JObject> GetRepositoryInfoAsync(string repository, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(GitHubContentRepository.HttpClientName);
        using var response = await client.GetAsync($"repos/{repository}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode ? JObject.Parse(details) : throw Failed(response, details, $"read repository \"{repository}\"");
    }

    public virtual Task CheckOrganizationAsync(CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(_options.Organization) ? Task.CompletedTask : EnsureOrganizationReachableAsync(cancellationToken);

    private async Task EnsureOrganizationReachableAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(GitHubContentRepository.HttpClientName);
        using var response = await client.GetAsync($"orgs/{_options.Organization.Trim()}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"The GitHub organization \"{_options.Organization}\" was not found, or the platform has no access to it.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw Failed(response, await response.Content.ReadAsStringAsync(cancellationToken), $"read organization \"{_options.Organization}\"");
        }
    }

    private async Task PatchRepositoryAsync(string repository, JObject changes, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(GitHubContentRepository.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"repos/{repository}") { Content = JsonContent(changes) };
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw Failed(response, await response.Content.ReadAsStringAsync(cancellationToken), $"update repository \"{repository}\"");
        }
    }

    private static StringContent JsonContent(JObject body) => new(body.ToString(), Encoding.UTF8, "application/json");

    private static HttpRequestException Failed(HttpResponseMessage response, string details, string operation)
    {
        if (details?.Length > 500)
        {
            details = details[..500];
        }

        return new HttpRequestException($"GitHub request to {operation} failed: {(int)response.StatusCode} {response.ReasonPhrase}. {details}", null, response.StatusCode);
    }
}
