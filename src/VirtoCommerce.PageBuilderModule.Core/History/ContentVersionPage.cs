using Newtonsoft.Json;

namespace VirtoCommerce.PageBuilderModule.Core.History;

/// <summary>A page of versions, newest first.</summary>
public class ContentVersionPage
{
    public ContentHistoryStatus Status { get; set; }

    public IList<ContentVersion> Versions { get; set; } = [];

    /// <summary>Continues the list with older versions; absent when there are no more.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Cursor { get; set; }
}
