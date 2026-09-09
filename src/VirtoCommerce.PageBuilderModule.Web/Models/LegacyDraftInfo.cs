using System;

namespace VirtoCommerce.PageBuilderModule.Web.Models
{
    /// <summary>
    /// What is known about one blob draft file left over from the flow that came before git.
    /// <para>
    /// <see cref="ExistsInGit"/> and <see cref="DiffersFromGit"/> are what make the file safe or unsafe to
    /// delete, and they are reported rather than acted on: only a person can decide that a draft nobody
    /// published is not worth keeping.
    /// </para>
    /// </summary>
    public class LegacyDraftInfo
    {
        /// <summary>False when the page has no leftover draft blob at all — the ordinary case.</summary>
        public bool Exists { get; set; }

        /// <summary>Blob path of the draft, relative to the store's content root. What delete takes.</summary>
        public string BlobPath { get; set; }

        /// <summary>Where the same page lives in the content repository.</summary>
        public string RepoPath { get; set; }

        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// Whether the repository has this page at all. When it does not, this blob is what the builder
        /// still serves, and deleting it loses the page rather than tidying up after it.
        /// </summary>
        public bool ExistsInGit { get; set; }

        /// <summary>
        /// Whether the draft still says something the repository does not — compared as documents, so
        /// the indent and line endings the old flow used do not count as a change.
        /// </summary>
        public bool DiffersFromGit { get; set; }
    }
}
