using System;
using System.Globalization;
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
    /// store's own opt-in. The store setting is read last because it is the only one that costs a query.
    /// </summary>
    public class GitContentPolicy : IGitContentPolicy
    {
        private readonly GitContentOptions _options;
        private readonly ISettingsManager _settingsManager;

        public GitContentPolicy(IOptions<GitContentOptions> options, ISettingsManager settingsManager)
        {
            _options = options.Value;
            _settingsManager = settingsManager;
        }

        public async Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled || !_options.IsConfigured || string.IsNullOrEmpty(storeId))
            {
                return false;
            }

            var setting = await _settingsManager.GetObjectSettingAsync(
                ModuleConstants.Settings.StoreLevelSettings.GitContentEnabled.Name,
                ModuleConstants.Settings.StoreSettingsObjectType,
                storeId);

            return ToBoolean(setting?.Value);
        }

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
