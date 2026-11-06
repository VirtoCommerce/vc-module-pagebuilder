using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using VirtoCommerce.Platform.Core.JsonConverters;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>The body of an answer as the platform writes it, with the JSON settings of its MVC.</summary>
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

    /// <summary>The body of an action's answer, whether the action returns a typed result or not.</summary>
    public static string SerializeBody(object result)
    {
        var actionResult = result is IConvertToActionResult typed ? typed.Convert() : result;
        return Serialize(Assert.IsAssignableFrom<ObjectResult>(actionResult).Value);
    }

    public static JObject ParseBody(object result) => JObject.Parse(SerializeBody(result));
}
