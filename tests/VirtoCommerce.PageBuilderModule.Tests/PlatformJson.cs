using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using VirtoCommerce.Platform.Core.JsonConverters;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal static class PlatformJson
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new PolymorphJsonContractResolver(),
        Converters = [new StringEnumConverter()],
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None,
    };

    public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

    public static string SerializeBody(object result)
    {
        var actionResult = result is IConvertToActionResult typed ? typed.Convert() : result;
        return Serialize(Assert.IsAssignableFrom<ObjectResult>(actionResult).Value);
    }

    public static JObject ParseBody(object result) => JObject.Parse(SerializeBody(result));
}
