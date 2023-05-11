using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Data.Search;
using VirtoCommerce.SearchModule.Core.Extenstions;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Data.Search
{
    public class PageBuilderContentItemBuilder : BaseContentItemBuilder
    {
        protected override IndexDocument BuildIndexDocumentInternal(string documentId, string storeId, IndexableContentFile file)
        {
            var result = new IndexDocument(documentId);
            result.AddFilterableAndSearchableValue("StoreId", storeId);

            var page = JsonConvert.DeserializeObject<JObject>(file.Content);

            AddMetadata(result, (JObject)page["settings"]);

            result.AddSearchableValue(page["content"].ToString());

            return result;
        }

        private static void AddMetadata(IndexDocument result, JObject settings)
        {
            settings.Properties().ToList().ForEach(x =>
            {
                result.AddFilterableAndSearchableValue(x.Name, x.Value.ToString());
            });
        }
    }
}