using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public interface IGitContentProvisioner
{
    /// <summary>Errors are returned in <see cref="GitContentSiteStatus.Error"/>.</summary>
    Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default);

    Task CheckOrganizationAsync(CancellationToken cancellationToken = default);

    Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default);

    Task ConnectAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>The repository is kept.</summary>
    Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Archives the repository only if it is marked as the deleted store's.</summary>
    Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default);
}
