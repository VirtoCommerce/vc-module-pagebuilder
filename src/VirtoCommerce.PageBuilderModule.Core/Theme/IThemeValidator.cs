using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeValidator
{
    /// <summary>A null catalog checks the shape only.</summary>
    IList<string> Validate(ThemeSource source, JObject catalog);
}
