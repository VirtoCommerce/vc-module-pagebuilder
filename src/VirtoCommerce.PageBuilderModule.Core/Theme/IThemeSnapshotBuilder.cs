using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeSnapshotBuilder
{
    string GetTemplateKey(string fileName);

    /// <summary>A null catalog checks the shape only.</summary>
    IList<string> Validate(ThemeSource source, JObject catalog);

    ThemeSnapshotSet Build(ThemeSource source, JObject catalog, string catalogHash, IList<string> languages, string defaultLanguage, int publication, DateTime publishedDate);
}
