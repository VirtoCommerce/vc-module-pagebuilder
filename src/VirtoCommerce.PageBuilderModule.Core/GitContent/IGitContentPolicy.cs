using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// The single place that answers "is the git content flow on for this store?". Every branch in the
    /// module goes through it, so the flow can never be turned on by accident: it takes the global
    /// kill switch, the per-store opt-in AND a configured connection to all agree.
    /// <para>
    /// Deliberately not inferred from indirect signals (such as "a token is present"): a stray
    /// environment variable must not silently change what publishing means — from "write to production"
    /// to "open a pull request".
    /// </para>
    /// </summary>
    public interface IGitContentPolicy
    {
        /// <summary>
        /// True when <c>PageBuilder:GitContent:Enabled</c> is set, the connection is configured, and the
        /// store opted in via the <c>VirtoCommerce.PageBuilderModule.Store.GitContentEnabled</c> setting.
        /// A missing or unreadable store setting means "off".
        /// </summary>
        Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>Switches git content on or off for the store.</summary>
        Task SetEnabledForStoreAsync(string storeId, bool enabled, CancellationToken cancellationToken = default);

        /// <summary>Gets the store repository ("owner/name"); <c>null</c> when git content is off for the store.</summary>
        /// <exception cref="GitRepositoryOwnershipException">The repository was created for another store.</exception>
        Task<string> GetRepositoryAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>Gets the store repository with its source; <c>null</c> when git content is off for the store.</summary>
        /// <exception cref="GitRepositoryOwnershipException">The repository was created for another store.</exception>
        Task<StoreRepository> GetRepositoryInfoAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>Gets the store repository from settings, whether or not git content is on for the store.</summary>
        Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>Gets the repository of a store with the given repository setting, whether or not git content is on for the store.</summary>
        Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, string repositorySetting, CancellationToken cancellationToken = default);

        /// <summary>Checks whether the repository is in the configured organization.</summary>
        Task<bool> IsInOrganizationAsync(string repository, CancellationToken cancellationToken = default);
    }
}
