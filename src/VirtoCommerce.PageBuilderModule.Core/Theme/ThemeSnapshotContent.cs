using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemeSnapshotContent : ICloneable
{
    public int Format { get; set; }

    public int Publication { get; set; }

    public DateTime PublishedDate { get; set; }

    public string Theme { get; set; }

    public string CultureName { get; set; }

    public string CatalogHash { get; set; }

    public ThemeTemplateRules Rules { get; set; }

    public IDictionary<string, JObject> Templates { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public JObject Header { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public JObject Footer { get; set; }

    public ThemeBrand Brand { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ThemePalette Palette { get; set; }

    public IList<string> Menus { get; set; }

    public virtual object Clone()
    {
        var result = (ThemeSnapshotContent)MemberwiseClone();

        result.Rules = Rules?.CloneTyped();
        result.Templates = Templates?.ToDictionary(x => x.Key, x => (JObject)x.Value?.DeepClone());
        result.Header = (JObject)Header?.DeepClone();
        result.Footer = (JObject)Footer?.DeepClone();
        result.Brand = Brand?.CloneTyped();
        result.Palette = Palette?.CloneTyped();
        result.Menus = Menus?.ToList();

        return result;
    }
}
