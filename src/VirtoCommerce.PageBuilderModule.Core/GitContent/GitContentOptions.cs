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
        /// Token with contents read/write access to the repository (fine-grained PAT or
        /// installation token). A platform secret — keep it in environment/secret storage.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// The branch new work branches are forked from (and the one PRs target).
        /// </summary>
        public string BaseBranch { get; set; } = "master";

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
            if (!Enabled || IsConfigured)
            {
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
