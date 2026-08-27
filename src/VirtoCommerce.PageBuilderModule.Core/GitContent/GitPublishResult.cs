namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>What happened to a page that an editor asked to publish.</summary>
    public enum GitPublishState
    {
        /// <summary>The change is in the production branch. CI takes it from there to the environment.</summary>
        Merged,

        /// <summary>
        /// The pull request is open and set to merge itself once the required checks pass. The page is
        /// not live yet, and the UI must not pretend otherwise.
        /// </summary>
        Pending,

        /// <summary>
        /// The work branch holds nothing the production branch does not. Publishing an unchanged page
        /// is a no-op, not an error.
        /// </summary>
        AlreadyPublished,

        /// <summary>
        /// The page moved in the production branch while this draft was being written, and the two
        /// cannot be merged. The editor has to re-read the page — silently taking either side would
        /// throw away someone's work.
        /// </summary>
        Conflict,
    }

    public class GitPublishResult
    {
        public GitPublishState State { get; init; }

        /// <summary>Null when there was nothing to publish.</summary>
        public int? PullRequestNumber { get; init; }

        /// <summary>Link to the pull request, for the audit trail and for a human resolving a conflict.</summary>
        public string Url { get; init; }
    }
}
