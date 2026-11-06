using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace VirtoCommerce.PageBuilderModule.Core.History;

/// <summary>A page of versions, newest first.</summary>
public class ContentVersionPage
{
    // the platform writes enums in Pascal case; the contract names this one in camel case
    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public ContentHistoryStatus Status { get; set; }

    public IList<ContentVersion> Versions { get; set; } = [];

    /// <summary>Continues the list with older versions; <c>null</c> when there are no more.</summary>
    public string Cursor { get; set; }
}
