using Newtonsoft.Json;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemePalette : ICloneable
{
    public IDictionary<string, string> Light { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IDictionary<string, string> Dark { get; set; }

    public virtual object Clone()
    {
        var result = (ThemePalette)MemberwiseClone();

        result.Light = Light?.ToDictionary(x => x.Key, x => x.Value);
        result.Dark = Dark?.ToDictionary(x => x.Key, x => x.Value);

        return result;
    }
}
