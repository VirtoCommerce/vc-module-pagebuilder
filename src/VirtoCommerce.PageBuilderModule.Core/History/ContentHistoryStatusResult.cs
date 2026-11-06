using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace VirtoCommerce.PageBuilderModule.Core.History;

public class ContentHistoryStatusResult
{
    // the platform writes enums in Pascal case; the contract names this one in camel case
    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public ContentHistoryStatus Status { get; set; }
}
