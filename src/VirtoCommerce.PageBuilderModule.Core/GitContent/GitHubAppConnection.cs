namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>GitHub App created by the one-click setup.</summary>
public class GitHubAppConnection
{
    public string Organization { get; set; }

    public string AppId { get; set; }

    /// <summary>The App's url name: "github.com/apps/{slug}".</summary>
    public string AppSlug { get; set; }

    /// <summary>Private key (PEM). Stored encrypted.</summary>
    public string PrivateKey { get; set; }
}
