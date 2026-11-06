using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeSnapshotBuilder
{
    /// <summary>Gets the template key of a file in the templates folder; null when the file is not a template.</summary>
    string GetTemplateKey(string fileName);

    /// <summary>Validates the theme against the catalog; a null catalog checks the shape only. Returns the errors.</summary>
    IList<string> Validate(ThemeSource source, JObject catalog);

    /// <summary>Builds the snapshot of each language.</summary>
    ThemeSnapshotSet Build(ThemeSource source, JObject catalog, string catalogHash, IList<string> languages, string defaultLanguage, int publication, DateTime publishedDate);
}
