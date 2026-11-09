using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public class GitHubAppManifest
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("redirect_url")]
    public string RedirectUrl { get; set; }

    [JsonProperty("setup_url")]
    public string SetupUrl { get; set; }

    [JsonProperty("setup_on_update")]
    public bool SetupOnUpdate { get; set; }

    [JsonProperty("public")]
    public bool IsPublic { get; set; }

    [JsonProperty("default_permissions")]
    public IDictionary<string, string> DefaultPermissions { get; set; } = new Dictionary<string, string>();

    [JsonProperty("default_events")]
    public IList<string> DefaultEvents { get; set; } = [];
}
