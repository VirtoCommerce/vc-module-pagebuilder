using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// Three independent switches must all be on: the global kill switch, a usable connection, and the
    /// store's own opt-in. The store settings are read last because they are the only part that costs a query.
    /// </summary>
    public class GitContentPolicy : IGitContentPolicy
    {
        private static readonly string EnabledSetting = ModuleConstants.Settings.StoreLevelSettings.GitContentEnabled.Name;
        private static readonly string RepositorySetting = ModuleConstants.Settings.StoreLevelSettings.GitContentRepository.Name;

        private readonly GitContentOptions _options;
        private readonly ISettingsManager _settingsManager;

        public GitContentPolicy(IOptions<GitContentOptions> options, ISettingsManager settingsManager)
        {
            _options = options.Value;
            _settingsManager = settingsManager;
        }

        public async Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default) =>
            await GetRepositoryAsync(storeId, cancellationToken) != null;

        public async Task<string> GetRepositoryAsync(string storeId, CancellationToken cancellationToken = default)
        {
            var store = await ReadStoreAsync(storeId);
            return store is { Enabled: true } ? store.Repository : null;
        }

        public async Task<string> ResolveRepositoryAsync(string storeId, CancellationToken cancellationToken = default) =>
            (await ReadStoreAsync(storeId))?.Repository;

        /// <summary>Both store settings in one query, or <c>null</c> when the flow cannot be on for any store.</summary>
        private async Task<StoreGitSettings> ReadStoreAsync(string storeId)
        {
            if (!_options.Enabled || !_options.IsConfigured || string.IsNullOrEmpty(storeId))
            {
                return null;
            }

            var settings = await _settingsManager.GetObjectSettingsAsync(
                [EnabledSetting, RepositorySetting],
                ModuleConstants.Settings.StoreSettingsObjectType,
                storeId);

            object Value(string name) => settings?.FirstOrDefault(x => x.Name == name)?.Value;

            return new StoreGitSettings(
                ToBoolean(Value(EnabledSetting)),
                _options.RepositoryFor(storeId, Convert.ToString(Value(RepositorySetting), CultureInfo.InvariantCulture)));
        }

        private sealed record StoreGitSettings(bool Enabled, string Repository);

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
