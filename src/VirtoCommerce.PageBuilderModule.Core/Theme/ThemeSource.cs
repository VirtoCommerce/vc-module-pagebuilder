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

    public IDictionary<string, string> Drafts { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IList<string> InvalidFiles { get; set; } = new List<string>();
}
