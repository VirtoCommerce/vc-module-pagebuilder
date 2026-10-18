using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Each site's repository: created in the configured organization when the site needs one, switched
    /// on and off for the store, archived when the store goes away.
    /// <para>
    /// The module creates, adopts and archives repositories only inside <see cref="GitContentOptions.Organization"/>,
    /// and archives only the ones it created for that store — recognised by their description. A repository
    /// a store names in its own setting is used, never changed.
    /// </para>
    /// </summary>
    public interface IGitContentProvisioner
    {
        /// <summary>
        /// The store's git connection as GitHub sees it right now: always asked live, so it doubles as the
        /// connection test. Failures are reported in <see cref="GitContentSiteStatus.Error"/>, not thrown.
        /// </summary>
        Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asks GitHub for the organization with the platform's credentials: the connection test. Throws, with
        /// what to fix, when it is not there or the App is not installed on it.
        /// </summary>
        Task CheckOrganizationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes sure the store's repository exists, with the base branch (and the release branch, when one is
        /// configured) to work from. Safe to repeat: an existing repository is adopted, an archived one of the
        /// same store brought back.
        /// </summary>
        Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>The store's repository made ready, then the git flow switched on for the store.</summary>
        Task ConnectAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>The git flow switched off for the store. The repository and its history stay.</summary>
        Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Archives <paramref name="repository"/>, the repository of a store that was deleted — only when the
        /// module created it for that store. Anything else is left alone.
        /// </summary>
        Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default);
    }

    /// <summary>A store's git connection, for the site's Git panel.</summary>
    public sealed record GitContentSiteStatus
    {
        /// <summary>The platform has a usable GitHub connection; without it there is nothing else to report.</summary>
        public bool Configured { get; init; }

        public string Organization { get; init; }

        /// <summary>The store has the git flow switched on.</summary>
        public bool Enabled { get; init; }

        /// <summary>The store's repository ("owner/name"), whether or not it exists yet.</summary>
        public string Repository { get; init; }

        public string BaseBranch { get; init; }

        /// <summary>The repository on GitHub; <c>null</c> while it does not exist.</summary>
        public string Url { get; init; }

        /// <summary>Where the base branch points — the site's last published change.</summary>
        public string LastCommitSha { get; init; }

        /// <summary>Why GitHub could not be asked, in words an administrator can act on.</summary>
        public string Error { get; init; }

        /// <summary>
        /// Where the connection comes from: "configuration" (the environment's, which always wins), "app"
        /// (the one-click setup), or <c>null</c> while there is none.
        /// </summary>
        public string Source { get; init; }

        /// <summary>Where to install the one-click setup's App on its organization; <c>null</c> without one.</summary>
        public string InstallUrl { get; init; }
    }
}
