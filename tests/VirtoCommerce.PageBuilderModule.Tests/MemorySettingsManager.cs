using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>Platform-wide settings kept in a dictionary: what is saved is what is read back.</summary>
    public sealed class MemorySettingsManager : ISettingsManager
    {
        public Dictionary<string, object> Values { get; } = [];

        public Task<ObjectSettingEntry> GetObjectSettingAsync(string name, string objectType = null, string objectId = null) =>
            Task.FromResult(new ObjectSettingEntry { Name = name, Value = Values.GetValueOrDefault(name) });

        public async Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null) =>
            await Task.WhenAll(names.Select(name => GetObjectSettingAsync(name)));

        public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings)
        {
            foreach (var setting in objectSettings)
            {
                Values[setting.Name] = setting.Value;
            }

            return Task.CompletedTask;
        }

        public Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => throw new NotSupportedException();

        public IEnumerable<SettingDescriptor> AllRegisteredSettings => throw new NotSupportedException();

        public IEnumerable<SettingDescriptor> GetSettingsForType(string typeName) => throw new NotSupportedException();

        public IDictionary<string, string[]> GetSettingTypeAssignments() => throw new NotSupportedException();

        public void RegisterSettings(IEnumerable<SettingDescriptor> settings, string moduleId = null) => throw new NotSupportedException();

        public void RegisterSettingsForType(IEnumerable<SettingDescriptor> settings, string typeName) => throw new NotSupportedException();
    }
}
