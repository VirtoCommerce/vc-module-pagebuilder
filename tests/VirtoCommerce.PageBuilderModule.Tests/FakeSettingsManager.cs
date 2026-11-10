using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    internal sealed class FakeSettingsManager : ISettingsManager
    {
        private readonly object _value;
        private readonly string _repository;

        public FakeSettingsManager(object value = null, string repository = null)
        {
            _value = value;
            _repository = repository;
        }

        public Dictionary<string, object> Values { get; } = [];

        public Exception ReadFailure { get; init; }

        public string AskedName { get; private set; }
        public string AskedObjectType { get; private set; }
        public string AskedObjectId { get; private set; }

        public Task<ObjectSettingEntry> GetObjectSettingAsync(string name, string objectType = null, string objectId = null)
        {
            if (ReadFailure != null)
            {
                return Task.FromException<ObjectSettingEntry>(ReadFailure);
            }

            AskedName = name;
            AskedObjectType = objectType;
            AskedObjectId = objectId;

            return Task.FromResult(new ObjectSettingEntry { Name = name, Value = GetValue(name) });
        }

        public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null)
        {
            if (ReadFailure != null)
            {
                return Task.FromException<IEnumerable<ObjectSettingEntry>>(ReadFailure);
            }

            var asked = names.ToList();
            AskedName = asked.FirstOrDefault();
            AskedObjectType = objectType;
            AskedObjectId = objectId;

            return Task.FromResult<IEnumerable<ObjectSettingEntry>>(asked
                .Select(name => new ObjectSettingEntry { Name = name, Value = GetValue(name) })
                .ToList());
        }

        public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings)
        {
            // Like the platform, unregistered settings and settings without a value are not saved.
            foreach (var setting in objectSettings.Where(x => x.ItHasValues && Core.ModuleConstants.Settings.AllSettings.Any(descriptor => descriptor.Name == x.Name)))
            {
                Values[setting.Name] = setting.Value;
            }

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

        private object GetValue(string name)
        {
            if (Values.TryGetValue(name, out var value))
            {
                return value;
            }

            return name == Core.ModuleConstants.Settings.StoreLevelSettings.GitContentRepository.Name ? _repository : _value;
        }
    }
}
