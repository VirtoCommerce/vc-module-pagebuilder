using System;
using System.Collections.Generic;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// One version of a page: a commit that touched its file, wherever in the repository it lives.
    /// <para>
    /// A version is a commit, not a commit-on-a-branch. Work branches are cut from one another as often
    /// as from the production branch, so the same commit is reachable from many refs — on the content
    /// repository a single page edit was observed on fourteen branches at once. Collapsing them into one
    /// version and listing the refs that contain it is what keeps the list readable; the branch names are
    /// information about a version, never its identity.
    /// </para>
    /// </summary>
    public sealed class GitPageVersion
    {
        private const int ShortShaLength = 7;

        /// <summary>The commit sha — what a preview link and a restore are addressed by.</summary>
        public string Sha { get; init; }

        public string ShortSha => Sha != null && Sha.Length > ShortShaLength ? Sha[..ShortShaLength] : Sha;

        public DateTime? Date { get; init; }

        public string Message { get; init; }

        /// <summary>
        /// Git author of the commit. For commits this module made these are the platform user name and
        /// the configured fallback address, not a personal email — see <see cref="AuthorLogin"/>.
        /// </summary>
        public string AuthorName { get; init; }

        public string AuthorEmail { get; init; }

        /// <summary>
        /// GitHub account the commit's author email resolves to, when it resolves to one. Edits made
        /// outside the builder carry the person's own git identity and so have a login; the module's own
        /// commits are signed with a shared address and have none.
        /// </summary>
        public string AuthorLogin { get; init; }

        /// <summary>
        /// Platform login the server recorded in the commit message, and the only attribution a client
        /// cannot forge. <c>null</c> for commits made outside the module.
        /// </summary>
        public string VcUser { get; init; }

        /// <summary>Branches this commit is reachable from, production branch first when it is one of them.</summary>
        public IList<string> Branches { get; init; } = [];

        /// <summary>Whether the production branch contains this commit — "this version is live".</summary>
        public bool Published { get; init; }

        /// <summary>How many files the commit touched, when GitHub was willing to say.</summary>
        public int? ChangedFiles { get; init; }

        /// <summary>
        /// A commit that changed far more than this page — a seeding or bulk-reformat commit. Such a
        /// commit is a truthful version of the page and a useless one to a content manager: on the
        /// content repository the only unpublished "version" of several pages was a 366-file import.
        /// Flagged rather than dropped, so the list can play it down without hiding history.
        /// </summary>
        public bool Bulk { get; init; }
    }
}
