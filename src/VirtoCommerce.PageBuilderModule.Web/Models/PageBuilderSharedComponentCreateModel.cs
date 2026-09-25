using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Web.Models;

public class PageBuilderSharedComponentCreateModel
{
    public string StoreId { get; set; }

    public string Name { get; set; }

    public JObject Content { get; set; }
}
