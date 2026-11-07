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

    /// <summary>As a platform with <c>IncludeOutputNullValues</c> on writes the body.</summary>
    public static string SerializeBodyWithNulls(object result)
    {
        var actionResult = result is IConvertToActionResult typed ? typed.Convert() : result;
        var settings = new JsonSerializerSettings
        {
            ContractResolver = Settings.ContractResolver,
            Converters = Settings.Converters,
            NullValueHandling = NullValueHandling.Include,
        };
        return JsonConvert.SerializeObject(Assert.IsAssignableFrom<ObjectResult>(actionResult).Value, settings);
    }

    public static string SerializeBody(object result)
    {
        var actionResult = result is IConvertToActionResult typed ? typed.Convert() : result;
        return Serialize(Assert.IsAssignableFrom<ObjectResult>(actionResult).Value);
    }

    public static JObject ParseBody(object result) => JObject.Parse(SerializeBody(result));
}
