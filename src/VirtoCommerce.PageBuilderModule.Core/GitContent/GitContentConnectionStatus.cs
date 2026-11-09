namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public class GitContentConnectionStatus
{
    public IList<GitContentSite> Sites { get; set; } = [];

    public bool Configured { get; set; }

    public GitHubConnectionSource? Source { get; set; }

    public string Organization { get; set; }

    public string OrganizationUrl { get; set; }

    public string Repository { get; set; }

    public string AppSlug { get; set; }

    public string AppUrl { get; set; }

    public string InstallUrl { get; set; }

    public bool AutoCreateRepositories { get; set; }

    public string Error { get; set; }
}
