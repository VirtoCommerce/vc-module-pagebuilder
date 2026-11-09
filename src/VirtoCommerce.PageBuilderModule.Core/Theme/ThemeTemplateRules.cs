using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemeTemplateRules : ICloneable
{
    public int Version { get; set; }

    public IList<string> Conditions { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IList<JObject> Product { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IList<JObject> Category { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IList<JObject> Page { get; set; }

    public virtual object Clone()
    {
        var result = (ThemeTemplateRules)MemberwiseClone();

        result.Conditions = Conditions?.ToList();
        result.Product = Product?.Select(x => (JObject)x.DeepClone()).ToList();
        result.Category = Category?.Select(x => (JObject)x.DeepClone()).ToList();
        result.Page = Page?.Select(x => (JObject)x.DeepClone()).ToList();

        return result;
    }
}
