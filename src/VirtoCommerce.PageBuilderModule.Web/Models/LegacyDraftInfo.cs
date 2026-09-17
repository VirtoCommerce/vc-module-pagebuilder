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

        /// <summary>Whether the content repository has this page at all.</summary>
        public bool ExistsInGit { get; set; }

        /// <summary>
        /// Nothing but this file holds the page — it is in neither the repository nor a published blob.
        /// Deleting it then loses the page rather than tidying up after it, so the cleanup refuses.
        /// <para>
        /// Not the same as "absent from the repository": a store that opted into the git flow keeps every
        /// page it had, and one never re-saved from the designer lives in blob storage alone. There the
        /// published blob goes on serving the page once the draft is gone.
        /// </para>
        /// </summary>
        public bool IsOnlyCopy { get; set; }

        /// <summary>
        /// Whether the draft still says something the version that currently serves this page does not —
        /// the repository's copy where there is one, the published blob otherwise. Compared as documents,
        /// so the indent and line endings the old flow used do not count as a change.
        /// </summary>
        public bool DiffersFromCurrent { get; set; }
    }
}
