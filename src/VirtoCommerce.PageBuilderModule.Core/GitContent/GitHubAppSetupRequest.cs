namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>The one-click setup that connects the platform to a GitHub organization.</summary>
public class GitHubAppSetupRequest
{
    public string Organization { get; set; }

    /// <summary>The site the setup was started from, to come back to; without one, the Sites panel.</summary>
    public string StoreId { get; set; }
}
