using Newtonsoft.Json;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemeBrand : ICloneable
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string LogoUrl { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string SecondaryLogoUrl { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string FaviconUrl { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
