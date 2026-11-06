using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>GitHub connection in effect.</summary>
public class GitHubConnection
{
    public bool IsEnabled { get; set; }

    /// <summary>Shared repository ("owner/name") for all stores.</summary>
    public string Repository { get; set; }

    /// <summary>GitHub organization that holds one repository per store.</summary>
    public string Organization { get; set; }

    public string AppId { get; set; }

    public string PrivateKey { get; set; }

    public string Token { get; set; }

    public string BaseBranch { get; set; }

    public bool AutoCreateRepositories { get; set; }

    /// <summary>GitHub web site of the API.</summary>
    public Uri WebUrl { get; set; }

    /// <summary><c>null</c> while there is no connection.</summary>
    public GitHubConnectionSource? Source { get; set; }

    /// <summary>GitHub App of the one-click setup in effect; <c>null</c> when the configuration wins or there is none.</summary>
    public GitHubAppConnection App { get; set; }

    /// <summary>Why the saved GitHub App cannot be used.</summary>
    public string Error { get; set; }

    public bool UsesApp => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(PrivateKey);

    public bool IsConfigured =>
        (!string.IsNullOrWhiteSpace(Repository) || !string.IsNullOrWhiteSpace(Organization)) &&
        (!string.IsNullOrWhiteSpace(Token) || UsesApp) &&
        !string.IsNullOrWhiteSpace(BaseBranch);
}
