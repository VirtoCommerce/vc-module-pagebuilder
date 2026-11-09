namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public class GitContentSiteStatus
{
    public bool Configured { get; set; }

    public string Organization { get; set; }

    public bool Enabled { get; set; }

    public string Repository { get; set; }

    public string BaseBranch { get; set; }

    public string Url { get; set; }

    public string LastCommitSha { get; set; }

    public string Error { get; set; }

    public GitHubConnectionSource? Source { get; set; }

    public string InstallUrl { get; set; }
}
