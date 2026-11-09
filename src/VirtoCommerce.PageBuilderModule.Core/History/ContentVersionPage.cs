using Newtonsoft.Json;

namespace VirtoCommerce.PageBuilderModule.Core.History;

public class ContentVersionPage
{
    public ContentHistoryStatus Status { get; set; }

    public IList<ContentVersion> Versions { get; set; } = [];

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Cursor { get; set; }
}
