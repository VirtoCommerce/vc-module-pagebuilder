using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// Records the lookup the policy performs and returns one canned value. Only
    /// <see cref="GetObjectSettingAsync"/> is exercised; everything else throws, so a change that starts
    /// calling the settings store some other way fails loudly instead of passing silently.
    /// </summary>
    public class FakeSettingsManager : ISettingsManager
    {
        private readonly object _value;

        public FakeSettingsManager(object value) => _value = value;

        public string AskedName { get; private set; }
        public string AskedObjectType { get; private set; }
        public string AskedObjectId { get; private set; }

        public Task<ObjectSettingEntry> GetObjectSettingAsync(string name, string objectType = null, string objectId = null)
        {
            AskedName = name;
            AskedObjectType = objectType;
            AskedObjectId = objectId;

            return Task.FromResult(new ObjectSettingEntry { Name = name, Value = _value });
        }

        public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null) =>
            throw new NotSupportedException();

        public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) =>
            throw new NotSupportedException();

        public Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) =>
            throw new NotSupportedException();

        public IEnumerable<SettingDescriptor> AllRegisteredSettings => throw new NotSupportedException();

        public IEnumerable<SettingDescriptor> GetSettingsForType(string typeName) =>
            throw new NotSupportedException();

        public IDictionary<string, string[]> GetSettingTypeAssignments() =>
            throw new NotSupportedException();

        public void RegisterSettings(IEnumerable<SettingDescriptor> settings, string moduleId = null) =>
            throw new NotSupportedException();

        public void RegisterSettingsForType(IEnumerable<SettingDescriptor> settings, string typeName) =>
            throw new NotSupportedException();
    }
}
