using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>GitHub connection and site status of the Sites panel, and the one-click GitHub App setup.</summary>
public interface IGitContentSetupService
{
    /// <summary>Gets the GitHub connection status. <paramref name="live"/> also checks the organization on GitHub.</summary>
    Task<GitContentConnectionStatus> GetConnectionStatusAsync(bool live, CancellationToken cancellationToken = default);

    /// <summary>Gets the repository of each store and whether git content is on for it. Settings only, no GitHub calls.</summary>
    Task<IList<GitContentSite>> GetSitesAsync(IList<Store> stores, CancellationToken cancellationToken = default);

    /// <summary>Gets the git content status of the store with the GitHub connection it uses.</summary>
    Task<GitContentSiteStatus> GetSiteStatusAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether the one-click setup may set the connection up: not while the configuration is in charge.</summary>
    Task<bool> CanSetUpAppAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates the state that binds the GitHub callbacks to the setup.</summary>
    string CreateSetupState(GitHubAppSetupRequest setup);

    /// <summary>Reads the setup from its state; <c>null</c> when the state is expired or not valid.</summary>
    GitHubAppSetupRequest ReadSetupState(string state);

    /// <summary>Gets the GitHub page that creates the App of the setup from its manifest.</summary>
    Task<string> GetAppCreationUrlAsync(string organization, string state, CancellationToken cancellationToken = default);

    /// <summary>Gets the GitHub page that installs the App on its organization.</summary>
    Task<string> GetAppInstallUrlAsync(GitHubAppConnection app, CancellationToken cancellationToken = default);
}
