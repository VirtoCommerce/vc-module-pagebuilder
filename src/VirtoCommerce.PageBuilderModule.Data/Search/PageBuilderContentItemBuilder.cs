using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Data.Search;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Data.Search
{
    public class PageBuilderContentItemBuilder : BaseContentItemBuilder
    {
        protected override IndexDocument BuildIndexDocumentInternal(string documentId, string storeId, IndexableContentFile file)
        {
            var result = new IndexDocument(documentId);
            result.AddFilterableStringAndContentString("StoreId", storeId);

            var page = JsonConvert.DeserializeObject<JContainer>(file.Content);

            if (page is JArray pageAsArray)
            {
                AddMetadata(result, (JObject)pageAsArray.First());
                result.AddContentString(pageAsArray.Skip(1)?.ToString());
            }
            else
            {
                AddMetadata(result, (JObject)page["settings"]);
                result.AddContentString(page["content"]?.ToString());
            }

            return result;
        }

        private static void AddMetadata(IndexDocument result, JObject settings)
        {
            var dateConverter = new IsoDateTimeConverter { Culture = CultureInfo.InvariantCulture };
            settings.Properties().ToList().ForEach(x =>
            {
                result.AddFilterableStringAndContentString(x.Name, x.Value.ToString(Formatting.Indented, dateConverter));

            });
            if (settings["displayName"] == null && settings["name"] != null)
            {
                result.AddFilterableStringAndContentString("displayName", settings["name"].ToString());
            }
        }
    }
}
