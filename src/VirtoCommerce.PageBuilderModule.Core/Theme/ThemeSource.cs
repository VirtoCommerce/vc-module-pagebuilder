using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemeSource
{
    public string ThemeName { get; set; }

    public IDictionary<string, JToken> Templates { get; set; } = new Dictionary<string, JToken>();

    public JToken Rules { get; set; }

    public JToken Header { get; set; }

    public JToken Footer { get; set; }

    public JToken Brand { get; set; }

    public JToken SettingsData { get; set; }

    public JToken Palette { get; set; }

    public JToken PaletteDark { get; set; }

    /// <summary>Draft contents by file; the publication writes them live.</summary>
    public IDictionary<string, string> Drafts { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Files that are not valid JSON.</summary>
    public IList<string> InvalidFiles { get; set; } = new List<string>();
}
