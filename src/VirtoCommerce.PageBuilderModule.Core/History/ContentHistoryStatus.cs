using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace VirtoCommerce.PageBuilderModule.Core.History;

// the platform writes enums in Pascal case; the contract names this one in camel case
[JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
public enum ContentHistoryStatus
{
    /// <summary>History is kept.</summary>
    Enabled,

    /// <summary>The GitHub App is not connected, or the store's repository is not created yet.</summary>
    SetupRequired,

    /// <summary>The store's repository was assigned by hand: it keeps no history of database pages and theme files.</summary>
    Unavailable,
}
