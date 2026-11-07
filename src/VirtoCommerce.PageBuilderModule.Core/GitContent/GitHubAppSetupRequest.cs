namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>One-click GitHub App setup request.</summary>
public class GitHubAppSetupRequest
{
    public string Organization { get; set; }

    /// <summary>Store to return to after the setup.</summary>
    public string StoreId { get; set; }
}
