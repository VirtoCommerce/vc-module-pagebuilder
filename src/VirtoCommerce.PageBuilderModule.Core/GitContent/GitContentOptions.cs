using System.Text.RegularExpressions;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Connection to the content repositories for the git content flow: when it is on, a page save is a
    /// commit on a work branch (git is the source of truth) and publishing is a merge into
    /// <see cref="BaseBranch"/>, after which the platform writes the merged page to the live storage
    /// itself. Direct live writes are restricted to callers holding the publish permission.
    /// <para>
    /// Each site has its own repository: "{<see cref="Organization"/>}/{<see cref="RepositoryPrefix"/>}{storeId}",
    /// created by the module, or one the store names in its own setting. <see cref="Repository"/> is the
    /// single repository every store shared before, kept for installations that still use it.
    /// </para>
    /// <para>
    /// <see cref="Enabled"/> is a global kill switch, not the whole answer: a store also has to opt in
    /// through its own setting. Ask <see cref="IGitContentPolicy"/> rather than reading this flag.
    /// </para>
    /// </summary>
    public class GitContentOptions
    {
        public const string SectionName = "PageBuilder:GitContent";

        /// <summary>GitHub's limit on a repository name.</summary>
        public const int MaxRepositoryNameLength = 100;

        // "owner/name" as GitHub allows them; anything else in a store's setting ends up in api urls
        private static readonly Regex RepositoryPattern = new("^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$", RegexOptions.None, TimeSpan.FromSeconds(1));

        public bool Enabled { get; set; }

        /// <summary>
        /// The single content repository all stores share, in "owner/name" form — the setup before each
        /// site had its own. Mutually exclusive with <see cref="Organization"/>.
        /// </summary>
        public string Repository { get; set; }

        /// <summary>
        /// The GitHub organization that holds one repository per site. One per environment. Set here, in the
        /// environment's configuration, or by the one-click "Enable history" setup, which keeps it together
        /// with its App in a platform setting protected with this server's data protection keys — a
        /// production database restored into staging cannot read it there, so staging never creates or
        /// archives repositories in production's organization.
        /// </summary>
        public string Organization { get; set; }

        /// <summary>Prefix of the site repositories: "site-" names the repository of store "b2b-store" "site-b2b-store".</summary>
        public string RepositoryPrefix { get; set; } = "site-";

        /// <summary>
        /// A store created on the platform gets its repository and the git flow straight away. Existing
        /// stores are switched on one by one from their site page. Needs <see cref="Organization"/>.
        /// </summary>
        public bool AutoCreateRepositories { get; set; }

        /// <summary>
        /// Id of the GitHub App the module acts as. With <see cref="PrivateKey"/> it replaces
        /// <see cref="Token"/>: each request carries a short-lived token of the App's installation on the
        /// repository's owner, which is also what may create the site repositories.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// The App's private key in PEM form. A platform secret — keep it in environment/secret storage.
        /// "\n" escapes are accepted for environments that cannot hold line breaks.
        /// </summary>
        public string PrivateKey { get; set; }

        /// <summary>
        /// Base url of the GitHub REST API. Override for GitHub Enterprise.
        /// </summary>
        public Uri ApiUrl { get; set; } = new("https://api.github.com/");

        /// <summary>
        /// GitHub's GraphQL endpoint. Needed because auto-merge — letting a pull request merge itself
        /// once the required checks go green — exists only there; the REST API can merge immediately or
        /// not at all. Override for GitHub Enterprise.
        /// <para>
        /// Auto-merge also has to be allowed on the repository itself ("Allow auto-merge" in its
        /// settings). Without it, a publish whose merge a required check blocks can only be finished by
        /// asking again: the builder reports that page as awaiting a merge and keeps Publish available,
        /// rather than showing progress nothing is going to complete.
        /// </para>
        /// </summary>
        public Uri GraphQlUrl { get; set; } = new("https://api.github.com/graphql");

        /// <summary>
        /// Personal access token with contents and pull-request read/write access to the repositories,
        /// used when no GitHub App is configured. A platform secret — keep it in environment/secret storage.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// The branch new work branches are forked from (and the one PRs target).
        /// <para>
        /// Deliberately has no default. It must name the same branch the content repository's deploy
        /// workflow triggers on, and only the operator knows which that is — a default would be a guess
        /// that boots successfully, merging into one branch while CI listens to another and publishing
        /// silently stops arriving. Left unset with the flow enabled, <see cref="Validate"/> refuses to
        /// start, which is the whole point of that check.
        /// </para>
        /// </summary>
        public string BaseBranch { get; set; }

        /// <summary>
        /// The branch the production environment follows. Optional: left empty, this installation simply
        /// has no production promotion — the action is not offered and the endpoint refuses it, rather
        /// than promoting into whatever branch happened to be configured for something else.
        /// <para>
        /// Deliberately separate from <see cref="BaseBranch"/> rather than derived from it. Promotion is
        /// not a merge of one branch into the other: the two branches never share history, because a page
        /// reaches production as its own commit carrying that page's file state. Which is also why they
        /// must not name the same branch — see <see cref="Validate"/>.
        /// </para>
        /// </summary>
        public string ReleaseBranch { get; set; }

        /// <summary>
        /// Work-branch name, one per editor AND page: {user} is the sanitized user name, {slug} the
        /// flattened page path. The branch is cut from <see cref="BaseBranch"/> at the first save and
        /// deleted once the page is published, so the base is never merged into it and a page can never
        /// be published together with the editor's other unfinished work.
        /// <para>
        /// The page path is flattened into {slug} rather than nested with slashes: refs
        /// "designer/john/industry" and "designer/john/industry/manufacturing" cannot coexist — git will
        /// not let one ref be both a file and a directory.
        /// </para>
        /// </summary>
        public string BranchTemplate { get; set; } = "designer/{user}/{slug}";

        /// <summary>
        /// Branch name for a promotion to production. No {user} segment on purpose: promotion is per
        /// page, not per editor, so two people promoting the same page must land on the same branch and
        /// reuse the open pull request rather than opening a second one for the same file.
        /// <para>
        /// Being deterministic is also what makes a pending promotion findable: the status endpoint asks
        /// whether a pull request is open from exactly this branch.
        /// </para>
        /// </summary>
        public string PromoteBranchTemplate { get; set; } = "promote/{slug}";

        /// <summary>
        /// Repository-relative folder that holds the page files: a page saved by the builder with
        /// path "docs/foo.page" is committed as "{PagesRoot}/docs/foo.page".
        /// </summary>
        public string PagesRoot { get; set; } = "pages";

        /// <summary>
        /// Commit author used when the current user has no resolvable name/email.
        /// </summary>
        public string FallbackAuthorName { get; set; } = "VirtoCommerce Page Builder";

        public string FallbackAuthorEmail { get; set; } = "pagebuilder-noreply@virtocommerce.com";

        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long a page read by branch name is cached. Every template the designer opens is a GitHub
        /// call, so the cache is what keeps the flow inside the API rate limit. A branch moves, hence
        /// the short life; a write through this module drops the entry it invalidated straight away.
        /// </summary>
        public TimeSpan ReadCacheExpiration { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// How long a page read by commit sha is cached. A sha is immutable, so the only reason to
        /// expire it at all is to bound memory.
        /// </summary>
        public TimeSpan ImmutableReadCacheExpiration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// How far back the production branch is read when assembling a page's version list. This is not
        /// a display limit: the same commits are the set that tells an unpublished version from a
        /// published one, so a page with more publishes than this in its past could show an old published
        /// version as a draft.
        /// </summary>
        public int PublishedHistoryDepth { get; set; } = 50;

        /// <summary>
        /// How many branches are examined for unpublished versions. Branches are not filtered by name:
        /// a prefix filter costs the same and would hide drafts made on branches named anything else,
        /// which is the whole failure this feature exists to fix.
        /// </summary>
        public int MaxBranches { get; set; } = 100;

        /// <summary>
        /// How many commits touching the page are read per branch. Branches share history, so most of
        /// these are commits the production branch already has; the unpublished ones are the newest and
        /// come first.
        /// </summary>
        public int CommitsPerBranch { get; set; } = 10;

        /// <summary>
        /// From how many changed files a commit counts as a bulk change rather than an edit of this page
        /// (<see cref="GitPageVersion.Bulk"/>).
        /// </summary>
        public int BulkCommitFileCount { get; set; } = 20;

        /// <summary>
        /// How long a page's version list is cached. Short, because any push moves it; this module drops
        /// the entry itself whenever it commits.
        /// </summary>
        public TimeSpan HistoryCacheExpiration { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// GitHub's web site for <see cref="ApiUrl"/>: github.com for api.github.com, the server itself for
        /// GitHub Enterprise ("https://ghe.example/api/v3/" → "https://ghe.example/").
        /// </summary>
        public Uri WebUrl => string.Equals(ApiUrl.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
            ? new Uri("https://github.com/")
            : new Uri(ApiUrl.GetLeftPart(UriPartial.Authority) + "/");

        /// <summary>True when requests authenticate as a GitHub App rather than with <see cref="Token"/>.</summary>
        public bool UsesApp => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(PrivateKey);

        /// <summary>
        /// True when the connection is filled in well enough to talk to GitHub at all.
        /// </summary>
        public bool IsConfigured =>
            (!string.IsNullOrWhiteSpace(Repository) || !string.IsNullOrWhiteSpace(Organization)) &&
            (!string.IsNullOrWhiteSpace(Token) || UsesApp) &&
            !string.IsNullOrWhiteSpace(BaseBranch);

        /// <summary>
        /// The repository holding a store's pages: <paramref name="configured"/> (the store's own setting)
        /// when set, else the store's repository in <see cref="Organization"/>, else the shared
        /// <see cref="Repository"/>; <c>null</c> when there is none.
        /// <para>
        /// A configured value must be a plain "owner/name" and is refused otherwise, never replaced by the
        /// computed name: it is typed by an administrator and ends up in api urls, where anything else
        /// could address a different GitHub endpoint with the module's credentials.
        /// </para>
        /// </summary>
        public string RepositoryFor(string storeId, string configured = null)
        {
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var repository = configured.Trim();
                var name = repository[(repository.IndexOf('/') + 1)..];
                if (!RepositoryPattern.IsMatch(repository) || name is "." or "..")
                {
                    throw new InvalidOperationException($"\"{configured}\" is not a GitHub repository in \"owner/name\" form.");
                }

                return repository;
            }

            if (!string.IsNullOrWhiteSpace(Organization))
            {
                var name = GitPageLocation.SanitizeRefComponent($"{RepositoryPrefix}{storeId}") ?? "site";
                if (name.Length > MaxRepositoryNameLength)
                {
                    name = name[..MaxRepositoryNameLength].Trim('-', '.');
                }

                return $"{Organization.Trim()}/{name}";
            }

            return string.IsNullOrWhiteSpace(Repository) ? null : Repository.Trim();
        }

        /// <summary>
        /// True for a repository in <see cref="Organization"/> — the only place the module creates,
        /// adopts or archives repositories.
        /// </summary>
        public bool IsInOrganization(string repository) =>
            !string.IsNullOrWhiteSpace(Organization) &&
            repository?.StartsWith($"{Organization.Trim()}/", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Fail fast at start-up: a half-configured git flow must not boot. Silently falling back to the
        /// blob flow would mean publishing writes straight to production on an installation whose
        /// operator believes every change goes through a pull request.
        /// </summary>
        public void Validate()
        {
            if (!Enabled)
            {
                return;
            }

            // Both would leave it to chance which repository a store's pages go to. Moving from the shared
            // repository to one per site is done store by store, by naming the shared one in each store's
            // own setting first.
            if (!string.IsNullOrWhiteSpace(Repository) && !string.IsNullOrWhiteSpace(Organization))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:{nameof(Repository)} and {SectionName}:{nameof(Organization)} are both set. " +
                    $"Use {nameof(Organization)} for one repository per site; to keep a store on the shared repository, " +
                    $"set it as that store's Git repository, then remove {nameof(Repository)}.");
            }

            if (IsConfigured)
            {
                // One branch on both ends turns promotion into a no-op that reports success: the page is
                // written back to the branch it already came from, CI redeploys the environment it was
                // already on, and production never hears about it. There is no configuration in which
                // this is meant, so it is a start-up failure rather than a surprise at the first promotion.
                if (!string.IsNullOrWhiteSpace(ReleaseBranch) &&
                    string.Equals(ReleaseBranch.Trim(), BaseBranch?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"{SectionName}:{nameof(ReleaseBranch)} and {SectionName}:{nameof(BaseBranch)} both name " +
                        $"\"{BaseBranch}\". Promotion to production would merge that branch into itself. " +
                        $"Name the production branch, or leave {nameof(ReleaseBranch)} empty to disable promotion.");
                }

                return;
            }

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(Repository) && string.IsNullOrWhiteSpace(Organization))
            {
                missing.Add($"{nameof(Organization)} (or {nameof(Repository)})");
            }
            if (string.IsNullOrWhiteSpace(Token) && !UsesApp)
            {
                missing.Add($"{nameof(AppId)} and {nameof(PrivateKey)} (or {nameof(Token)})");
            }
            if (string.IsNullOrWhiteSpace(BaseBranch))
            {
                missing.Add(nameof(BaseBranch));
            }

            throw new InvalidOperationException(
                $"{SectionName}:Enabled is set but the connection is incomplete — missing: {string.Join(", ", missing)}. " +
                $"Fill them in, or set {SectionName}:Enabled to false.");
        }
    }
}
