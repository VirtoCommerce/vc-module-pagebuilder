using Newtonsoft.Json;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

public class GitHubRepositoryInfo
{
    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("archived")]
    public bool Archived { get; set; }

    [JsonProperty("default_branch")]
    public string DefaultBranch { get; set; }

    [JsonProperty("html_url")]
    public string HtmlUrl { get; set; }
}
