namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Git content status of a store.</summary>
public class GitContentSiteStatus
{
    public bool Configured { get; set; }

    public string Organization { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Repository in "owner/name" form.</summary>
    public string Repository { get; set; }

    public string BaseBranch { get; set; }

    public string Url { get; set; }

    /// <summary>Head of the base branch.</summary>
    public string LastCommitSha { get; set; }

    public string Error { get; set; }

    public GitHubConnectionSource? Source { get; set; }

    /// <summary>GitHub App installation url.</summary>
    public string InstallUrl { get; set; }
}
