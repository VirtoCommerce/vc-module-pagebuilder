namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Connection to the content repository for the git content flow: when it is on, a page save is a
    /// commit on a work branch (git is the source of truth) and publishing is a merge into
    /// <see cref="BaseBranch"/>, performed by CI. Direct live writes are restricted to callers holding
    /// the publish permission (the CI deploy).
    /// <para>
    /// <see cref="Enabled"/> is a global kill switch, not the whole answer: a store also has to opt in
    /// through its own setting. Ask <see cref="IGitContentPolicy"/> rather than reading this flag.
    /// </para>
    /// </summary>
    public class GitContentOptions
    {
        public const string SectionName = "PageBuilder:GitContent";

        public bool Enabled { get; set; }

        /// <summary>
        /// Content repository in "owner/name" form, e.g. "VirtoCommerce/vc-content".
        /// </summary>
        public string Repository { get; set; }

        /// <summary>
        /// Base url of the GitHub REST API. Override for GitHub Enterprise.
        /// </summary>
        public Uri ApiUrl { get; set; } = new("https://api.github.com/");

        /// <summary>
        /// GitHub's GraphQL endpoint. Needed because auto-merge — letting a pull request merge itself
        /// once the required checks go green — exists only there; the REST API can merge immediately or
        /// not at all. Override for GitHub Enterprise.
        /// </summary>
        public Uri GraphQlUrl { get; set; } = new("https://api.github.com/graphql");

        /// <summary>
        /// Token with contents and pull-request read/write access to the repository (fine-grained PAT or
        /// GitHub App installation token). A platform secret — keep it in environment/secret storage.
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
        /// True when the connection is filled in well enough to talk to GitHub at all.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Repository) &&
            !string.IsNullOrWhiteSpace(Token) &&
            !string.IsNullOrWhiteSpace(BaseBranch);

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
            if (string.IsNullOrWhiteSpace(Repository))
            {
                missing.Add(nameof(Repository));
            }
            if (string.IsNullOrWhiteSpace(Token))
            {
                missing.Add(nameof(Token));
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
