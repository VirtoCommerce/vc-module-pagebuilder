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
        /// A missing or unreadable opt-in means "off".
        /// </summary>
        /// <exception cref="GitContentException">The store opted in, but its repository setting is not "owner/name" or the saved GitHub App cannot be read: git content is neither on nor safely off for it.</exception>
        Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default);

        Task SetEnabledForStoreAsync(string storeId, bool enabled, CancellationToken cancellationToken = default);

        /// <summary>Returns <c>null</c> when git content is off for the store.</summary>
        Task<string> GetRepositoryAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>Returns <c>null</c> when git content is off for the store.</summary>
        Task<StoreRepository> GetRepositoryInfoAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>Returns the repository whether or not git content is on for the store.</summary>
        /// <exception cref="GitContentException">The store setting is not a repository in "owner/name" form.</exception>
        Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, CancellationToken cancellationToken = default);

        /// <exception cref="GitContentException">The setting is not a repository in "owner/name" form.</exception>
        Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, string repositorySetting, CancellationToken cancellationToken = default);

        Task<bool> IsInOrganizationAsync(string repository, CancellationToken cancellationToken = default);
    }
}
