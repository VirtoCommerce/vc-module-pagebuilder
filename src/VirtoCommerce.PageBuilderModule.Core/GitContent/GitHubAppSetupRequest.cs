namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public class GitHubAppSetupRequest
{
    public string Organization { get; set; }

    /// <summary>
    /// The admin page that started the setup, a path on this platform: GitHub sends the browser back to it once the
    /// app is installed.
    /// </summary>
    public string ReturnUrl { get; set; }
}
