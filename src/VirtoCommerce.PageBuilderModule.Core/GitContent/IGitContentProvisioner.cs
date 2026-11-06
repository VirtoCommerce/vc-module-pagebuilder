using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Manages store content repositories on GitHub.</summary>
public interface IGitContentProvisioner
{
    /// <summary>Gets the store git status from GitHub. Errors are returned in <see cref="GitContentSiteStatus.Error"/>.</summary>
    Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Checks that the configured organization is reachable.</summary>
    Task CheckOrganizationAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or adopts the store repository with its base and release branches.</summary>
    Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Ensures the store repository and enables git content for the store.</summary>
    Task ConnectAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Disables git content for the store. The repository is kept.</summary>
    Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Archives the repository of a deleted store if the module created it for that store.</summary>
    Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default);
}
