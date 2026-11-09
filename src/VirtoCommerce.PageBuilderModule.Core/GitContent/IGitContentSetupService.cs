using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public interface IGitContentSetupService
{
    Task<GitContentConnectionStatus> GetConnectionStatusAsync(bool live, CancellationToken cancellationToken = default);

    /// <summary>Reads settings only, with no GitHub calls.</summary>
    Task<IList<GitContentSite>> GetSitesAsync(IList<Store> stores, CancellationToken cancellationToken = default);

    Task<GitContentSiteStatus> GetSiteStatusAsync(string storeId, CancellationToken cancellationToken = default);

    Task<bool> CanSetUpAppAsync(CancellationToken cancellationToken = default);

    string CreateSetupState(GitHubAppSetupRequest setup);

    /// <summary>Returns <c>null</c> when the state is expired or not valid.</summary>
    GitHubAppSetupRequest ReadSetupState(string state);

    Task<string> GetAppCreationUrlAsync(string organization, string state, CancellationToken cancellationToken = default);

    Task<string> GetAppInstallUrlAsync(GitHubAppConnection app, CancellationToken cancellationToken = default);
}
