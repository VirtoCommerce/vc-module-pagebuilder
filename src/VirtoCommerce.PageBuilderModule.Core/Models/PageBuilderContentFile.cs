using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderContentFile
{
    public string ContentType { get; set; }

    public string Path { get; set; }

    public JToken Content { get; set; }
}
