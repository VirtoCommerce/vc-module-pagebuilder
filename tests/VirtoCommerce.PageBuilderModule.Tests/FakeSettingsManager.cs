using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// Records the lookup the policy performs and answers it: <c>value</c> for the store's git switch,
    /// <c>repository</c> for its own repository setting. Only the two lookups are exercised; everything else
    /// throws, so a change that starts calling the settings store some other way fails loudly instead of
    /// passing silently.
    /// </summary>
    public class FakeSettingsManager : ISettingsManager
    {
        private readonly object _value;
        private readonly string _repository;

        public FakeSettingsManager(object value, string repository = null)
        {
            _value = value;
            _repository = repository;
        }

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

        public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null)
        {
            var asked = names.ToList();
            AskedName = asked.FirstOrDefault();
            AskedObjectType = objectType;
            AskedObjectId = objectId;

            return Task.FromResult<IEnumerable<ObjectSettingEntry>>(asked
                .Select(name => new ObjectSettingEntry
                {
                    Name = name,
                    Value = name == Core.ModuleConstants.Settings.StoreLevelSettings.GitContentRepository.Name ? _repository : _value,
                })
                .ToList());
        }

        /// <summary>What was saved — the store switch, when the provisioner turns the flow on or off.</summary>
        public List<ObjectSettingEntry> Saved { get; } = [];

        public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings)
        {
            Saved.AddRange(objectSettings);
            return Task.CompletedTask;
        }

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
