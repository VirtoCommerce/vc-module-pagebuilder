namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>GitHub connection of the platform, with the sites of the Sites panel.</summary>
public class GitContentConnectionStatus
{
    public IList<GitContentSite> Sites { get; set; } = [];

    public bool Configured { get; set; }

    public GitHubConnectionSource? Source { get; set; }

    public string Organization { get; set; }

    public string OrganizationUrl { get; set; }

    /// <summary>Shared repository of all stores; <c>null</c> with an organization.</summary>
    public string Repository { get; set; }

    public string AppSlug { get; set; }

    /// <summary>Settings page of the GitHub App.</summary>
    public string AppUrl { get; set; }

    /// <summary>GitHub App installation url.</summary>
    public string InstallUrl { get; set; }

    public bool AutoCreateRepositories { get; set; }

    public string Error { get; set; }
}
