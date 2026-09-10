using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Web.Models
{
    /// <summary>
    /// A page's versions as the builder sees them: the repository's answer plus the one thing only the
    /// server can add — which of them are the current editor's own.
    /// </summary>
    public class PageHistoryModel
    {
        public IList<PageVersionModel> Versions { get; set; } = [];

        /// <summary>
        /// The repository has more branches than this answer looked at, so there may be unpublished
        /// versions it does not list. Passed on rather than hidden: a list that is quietly incomplete
        /// reads as "there is nothing else".
        /// </summary>
        public bool Truncated { get; set; }

        /// <summary>Pass back as <c>after</c> to continue the branch scan.</summary>
        public string EndCursor { get; set; }

        /// <summary>
        /// Unpublished versions that are not this editor's own and not bulk changes — what the toolbar
        /// counts on its badge. Their own draft is what the designer already shows them, and a
        /// repository-wide import is nobody's draft of this page, so neither belongs in a number that
        /// means "someone else has unpublished work here".
        /// </summary>
        public int OtherDraftCount { get; set; }

        /// <summary>The branch this editor's own draft of the page lives on, whether or not it exists yet.</summary>
        public string MyDraftBranch { get; set; }

        public static PageHistoryModel Empty => new();

        public static PageHistoryModel From(GitPageHistory history, string myBranch)
        {
            ArgumentNullException.ThrowIfNull(history);

            var versions = history.Versions
                .Select(version => PageVersionModel.From(version, myBranch))
                .ToList();

            return new PageHistoryModel
            {
                Versions = versions,
                Truncated = history.Truncated,
                EndCursor = history.EndCursor,
                OtherDraftCount = versions.Count(version => !version.Published && !version.Mine && !version.Bulk),
                MyDraftBranch = myBranch,
            };
        }
    }

    public class PageVersionModel
    {
        public string Sha { get; set; }

        public string ShortSha { get; set; }

        public DateTime? Date { get; set; }

        public string Message { get; set; }

        public PageVersionAuthorModel Author { get; set; }

        /// <summary>Platform login the server recorded in the commit; absent for edits made outside the module.</summary>
        public string VcUser { get; set; }

        public IList<string> Branches { get; set; } = [];

        public bool Published { get; set; }

        /// <summary>
        /// This version is on the current editor's own work branch. Computed from the branch name, which
        /// is derived from their login — so it is exact for anything the builder committed, and false for
        /// an edit they made outside it under a different git identity (see the identity increment).
        /// </summary>
        public bool Mine { get; set; }

        /// <summary>A commit that changed far more than this page — an import or a bulk reformat.</summary>
        public bool Bulk { get; set; }

        public int? ChangedFiles { get; set; }

        public static PageVersionModel From(GitPageVersion version, string myBranch) => new()
        {
            Sha = version.Sha,
            ShortSha = version.ShortSha,
            Date = version.Date,
            Message = version.Message,
            Author = new PageVersionAuthorModel
            {
                Name = version.AuthorName,
                Email = version.AuthorEmail,
                Login = version.AuthorLogin,
            },
            VcUser = version.VcUser,
            Branches = version.Branches,
            Published = version.Published,
            Mine = !string.IsNullOrEmpty(myBranch) && version.Branches.Contains(myBranch, StringComparer.Ordinal),
            Bulk = version.Bulk,
            ChangedFiles = version.ChangedFiles,
        };
    }

    public class PageVersionAuthorModel
    {
        public string Name { get; set; }

        public string Email { get; set; }

        /// <summary>GitHub account the author email resolves to, when it resolves to one.</summary>
        public string Login { get; set; }
    }
}
