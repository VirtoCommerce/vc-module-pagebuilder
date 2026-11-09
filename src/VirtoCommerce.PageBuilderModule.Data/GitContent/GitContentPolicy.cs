using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using StoreLevelSettings = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Settings.StoreLevelSettings;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// Three independent switches must all be on: the global kill switch, a usable connection, and the
    /// store's own opt-in.
    /// </summary>
    public class GitContentPolicy : IGitContentPolicy
    {
        /// <summary>GitHub's limit on a repository name.</summary>
        public const int MaxRepositoryNameLength = 100;

        private const string DefaultRepositoryName = "site";

        private static readonly Regex RepositoryPattern = new("^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$", RegexOptions.None, TimeSpan.FromSeconds(1));

        private readonly IGitHubConnectionService _gitHubConnectionService;
        private readonly GitContentOptions _options;
        private readonly ISettingsManager _settingsManager;
        private readonly IGitContentOwnership _ownership;

        public GitContentPolicy(
            IGitHubConnectionService gitHubConnectionService,
            IOptions<GitContentOptions> options,
            ISettingsManager settingsManager,
            IGitContentOwnership ownership)
        {
            _gitHubConnectionService = gitHubConnectionService;
            _options = options.Value;
            _settingsManager = settingsManager;
            _ownership = ownership;
        }

        public virtual async Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default)
        {
            var connection = await _gitHubConnectionService.GetConnectionAsync(cancellationToken);
            var settings = await GetStoreSettingsAsync(connection, storeId);

            return settings is { IsEnabled: true } && GetStoreRepository(connection, storeId, settings.Repository) != null;
        }

        public virtual async Task<string> GetRepositoryAsync(string storeId, CancellationToken cancellationToken = default)
        {
            var repository = await GetRepositoryInfoAsync(storeId, cancellationToken);
            return repository?.Name;
        }

        public virtual async Task<StoreRepository> GetRepositoryInfoAsync(string storeId, CancellationToken cancellationToken = default)
        {
            var connection = await _gitHubConnectionService.GetConnectionAsync(cancellationToken);
            if (await GetStoreSettingsAsync(connection, storeId) is not { IsEnabled: true } settings)
            {
                return null;
            }

            var repository = GetStoreRepository(connection, storeId, settings.Repository);
            if (repository != null && connection.IsInOrganization(repository.Name))
            {
                var owner = await _ownership.GetStoreIdAsync(repository.Name, cancellationToken);
                _ownership.EnsureOwnedBy(repository.Name, owner, storeId);
            }

            return repository;
        }

        public virtual async Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, CancellationToken cancellationToken = default)
        {
            var connection = await _gitHubConnectionService.GetConnectionAsync(cancellationToken);
            var settings = await GetStoreSettingsAsync(connection, storeId);

            return settings == null ? null : GetStoreRepository(connection, storeId, settings.Repository);
        }

        public virtual async Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, string repositorySetting, CancellationToken cancellationToken = default)
        {
            var connection = await _gitHubConnectionService.GetConnectionAsync(cancellationToken);

            return IsAvailable(connection) ? GetStoreRepository(connection, storeId, repositorySetting) : null;
        }

        protected virtual StoreRepository GetStoreRepository(GitHubConnection connection, string storeId, string repositorySetting)
        {
            if (!string.IsNullOrWhiteSpace(repositorySetting))
            {
                // a configured value ends up in API urls, so only a plain "owner/name" is accepted
                var repository = repositorySetting.Trim();
                var name = repository[(repository.IndexOf('/') + 1)..];
                if (!RepositoryPattern.IsMatch(repository) || name is "." or "..")
                {
                    throw new GitContentException($"\"{repositorySetting}\" is not a GitHub repository in \"owner/name\" form.");
                }

                if (!IsAllowedRepository(connection, repository))
                {
                    throw new GitContentException($"\"{repository}\" is not a repository this platform may use. List it in {GitContentOptions.SectionName}:{nameof(GitContentOptions.AllowedRepositories)}.");
                }

                return new StoreRepository(repository, GitRepositorySource.StoreSetting);
            }

            if (!string.IsNullOrWhiteSpace(connection.Organization))
            {
                return new StoreRepository($"{connection.Organization.Trim()}/{GetRepositoryName(storeId)}", GitRepositorySource.Site);
            }

            return string.IsNullOrWhiteSpace(connection.Repository)
                ? null
                : new StoreRepository(connection.Repository.Trim(), GitRepositorySource.Shared);
        }

        protected virtual string GetRepositoryName(string storeId)
        {
            var name = GitPageLocation.SanitizeRefComponent($"{_options.RepositoryPrefix}{storeId}") ?? DefaultRepositoryName;

            return name.Length > MaxRepositoryNameLength
                ? name[..MaxRepositoryNameLength].Trim('-', '.')
                : name;
        }

        protected virtual bool IsAllowedRepository(GitHubConnection connection, string repository) =>
            string.Equals(repository, connection.Repository?.Trim(), StringComparison.OrdinalIgnoreCase) || _options.IsRepositoryAllowed(repository);

        private static bool IsAvailable(GitHubConnection connection) => connection.IsEnabled && connection.IsConfigured;

        private async Task<StoreGitSettings> GetStoreSettingsAsync(GitHubConnection connection, string storeId)
        {
            if (string.IsNullOrEmpty(storeId))
            {
                return null;
            }

            if (IsAvailable(connection))
            {
                return await ReadStoreSettingsAsync(storeId);
            }

            // the blob flow would publish an opted-in store's pages straight to production
            if (connection.IsEnabled && !string.IsNullOrEmpty(connection.Error) && (await ReadStoreSettingsAsync(storeId)).IsEnabled)
            {
                throw new GitContentException(connection.Error);
            }

            return null;
        }

        private async Task<StoreGitSettings> ReadStoreSettingsAsync(string storeId)
        {
            var settings = (await _settingsManager.GetObjectSettingsAsync(
                [StoreLevelSettings.GitContentEnabled.Name, StoreLevelSettings.GitContentRepository.Name],
                ModuleConstants.Settings.StoreSettingsObjectType,
                storeId))?.ToList() ?? [];

            var enabled = settings.FirstOrDefault(x => x.Name.EqualsIgnoreCase(StoreLevelSettings.GitContentEnabled.Name))?.Value;
            return new StoreGitSettings(ToBoolean(enabled), settings.GetValue<string>(StoreLevelSettings.GitContentRepository));
        }

        private sealed record StoreGitSettings(bool IsEnabled, string Repository);

        // The stored value arrives as whatever the settings backend deserialized it to (bool, string,
        // JValue). Anything we cannot read as a boolean means "not opted in" — never guess "on".
        private static bool ToBoolean(object value)
        {
            switch (value)
            {
                case null:
                    return false;
                case bool flag:
                    return flag;
                default:
                    return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed;
            }
        }
    }
}
