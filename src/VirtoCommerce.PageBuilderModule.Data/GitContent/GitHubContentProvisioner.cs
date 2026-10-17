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

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
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
        /// <summary>The start of the description of every repository the module creates; the store id follows.</summary>
        public const string Marker = "Page Builder content of store ";

        private const int BranchWaitAttempts = 10;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGitContentRepository _repository;
        private readonly IGitContentPolicy _policy;
        private readonly ISettingsManager _settingsManager;
        private readonly GitContentOptions _options;
        private readonly ILogger<GitHubContentProvisioner> _logger;

        public GitHubContentProvisioner(IHttpClientFactory httpClientFactory, IGitContentRepository repository, IGitContentPolicy policy,
            ISettingsManager settingsManager, IOptions<GitContentOptions> options, ILogger<GitHubContentProvisioner> logger)
        {
            _httpClientFactory = httpClientFactory;
            _repository = repository;
            _policy = policy;
            _settingsManager = settingsManager;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>How long to wait between looks for the first commit of a repository GitHub has just created.</summary>
        internal TimeSpan BranchWait { get; set; } = TimeSpan.FromSeconds(1);

        public async Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled || !_options.IsConfigured)
            {
                return new GitContentSiteStatus { Organization = _options.Organization, BaseBranch = _options.BaseBranch };
            }

            string repository = null;
            var enabled = false;
            try
            {
                repository = await _policy.ResolveRepositoryAsync(storeId, cancellationToken);
                enabled = await _policy.IsEnabledForStoreAsync(storeId, cancellationToken);

                var found = await GetRepositoryInfoAsync(repository, cancellationToken);
                if (found == null && _options.IsInOrganization(repository))
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
            catch (Exception failure) when (failure is HttpRequestException or InvalidOperationException)
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

        public async Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storeId);

            var repository = await _policy.ResolveRepositoryAsync(storeId, cancellationToken)
                             ?? throw new InvalidOperationException("Git content is not configured on this platform.");

            var found = await GetRepositoryInfoAsync(repository, cancellationToken);
            if (found == null)
            {
                if (!_options.IsInOrganization(repository))
                {
                    // never created outside the tenant organization: a store pointing elsewhere names a
                    // repository somebody already has
                    throw new InvalidOperationException($"The GitHub repository \"{repository}\" does not exist, or the platform has no access to it.");
                }

                found = await CreateRepositoryAsync(repository, storeId, cancellationToken);
            }

            if (_options.IsInOrganization(repository))
            {
                // Two stores whose ids sanitize alike ("B2B Store", "b2b-store") would share one repository
                // and publish over each other's pages. The description says whose it is.
                var owner = StoreOf(found);
                if (owner != null && !string.Equals(owner, storeId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The repository \"{repository}\" belongs to store \"{owner}\". Set this store's Git repository explicitly.");
                }

                if (found["archived"]?.Value<bool>() == true)
                {
                    // the store was deleted and has come back under the same id: its history is all there
                    await PatchRepositoryAsync(repository, new JObject { ["archived"] = false }, cancellationToken);
                }
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

        public async Task ConnectAsync(string storeId, CancellationToken cancellationToken = default)
        {
            await EnsureRepositoryAsync(storeId, cancellationToken);
            await SetEnabledAsync(storeId, true);
        }

        public Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default) =>
            SetEnabledAsync(storeId, false);

        public async Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default)
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

            if (!string.Equals(StoreOf(found), storeId, StringComparison.Ordinal))
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

        private static string StoreOf(JObject repository)
        {
            var description = repository["description"]?.Value<string>();
            return description?.StartsWith(Marker, StringComparison.Ordinal) == true ? description[Marker.Length..].Trim() : null;
        }

        private async Task<JObject> CreateRepositoryAsync(string repository, string storeId, CancellationToken cancellationToken)
        {
            var separator = repository.IndexOf('/');
            var body = new JObject
            {
                ["name"] = repository[(separator + 1)..],
                ["description"] = Marker + storeId,
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

        public Task CheckOrganizationAsync(CancellationToken cancellationToken = default) =>
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
}
