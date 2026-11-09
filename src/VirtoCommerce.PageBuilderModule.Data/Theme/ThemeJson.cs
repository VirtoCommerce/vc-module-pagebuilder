using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>Reads theme files and catalogs as written: a string that looks like a date stays that string, as the designer sends it.</summary>
public static class ThemeJson
{
    /// <exception cref="JsonReaderException">The text is not one JSON document.</exception>
    public static JToken Parse(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        var token = JToken.ReadFrom(reader);
        while (reader.Read())
        {
            // as JToken.Parse: the reader throws on anything but a comment after the document
        }

        return token;
    }

    /// <exception cref="JsonReaderException">The text is not one JSON object.</exception>
    public static JObject ParseObject(string json) =>
        Parse(json) as JObject ?? throw new JsonReaderException("The JSON document is not an object.");
}
