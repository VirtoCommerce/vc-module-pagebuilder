using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public class GitHubConnection
{
    public bool IsEnabled { get; set; }

    public string Repository { get; set; }

    public string Organization { get; set; }

    public string AppId { get; set; }

    public string PrivateKey { get; set; }

    public string Token { get; set; }

    public string BaseBranch { get; set; }

    public bool AutoCreateRepositories { get; set; }

    public Uri WebUrl { get; set; }

    public GitHubConnectionSource? Source { get; set; }

    /// <summary><c>null</c> when the configuration wins or there is none.</summary>
    public GitHubAppConnection App { get; set; }

    public string Error { get; set; }

    public bool UsesApp => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(PrivateKey);

    public bool IsConfigured =>
        (!string.IsNullOrWhiteSpace(Repository) || !string.IsNullOrWhiteSpace(Organization)) &&
        (!string.IsNullOrWhiteSpace(Token) || UsesApp) &&
        !string.IsNullOrWhiteSpace(BaseBranch);
}
