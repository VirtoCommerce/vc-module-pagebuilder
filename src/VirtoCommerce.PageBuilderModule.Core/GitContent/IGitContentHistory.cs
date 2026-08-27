using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// The versions of one page. Separate from <see cref="IGitContentRepository"/> on purpose: that one
    /// reads and writes a file at a ref, this one enumerates the repository to answer "where else does
    /// this page exist".
    /// </summary>
    public interface IGitContentHistory
    {
        /// <summary>
        /// Every commit that touched the page, published and unpublished, newest first with unpublished
        /// versions on top. Deduplicated by sha.
        /// </summary>
        Task<GitPageHistory> GetHistoryAsync(string repoPath, GitHistoryQuery query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forgets the cached history of the page. Called after this module commits to it: without it a
        /// save the editor just made would be missing from the version list until the cache expired, and
        /// the list would be denying work that is already in the repository.
        /// </summary>
        void Invalidate(string repoPath);
    }

    /// <summary>What to look at when assembling a page's history.</summary>
    public sealed class GitHistoryQuery
    {
        /// <summary>The branch that defines "published". Required.</summary>
        public string BaseBranch { get; set; }

        /// <summary>
        /// How far back to read the production branch. Doubles as the window that tells a draft from a
        /// published version, so it is not a display limit: see <see cref="GitContentOptions.PublishedHistoryDepth"/>.
        /// </summary>
        public int? PublishedDepth { get; set; }

        /// <summary>
        /// Branch cursor from a previous answer's <see cref="GitPageHistory.EndCursor"/> — the "show more"
        /// of a repository with more branches than one page of them.
        /// </summary>
        public string After { get; set; }
    }

    public sealed class GitPageHistory
    {
        public IList<GitPageVersion> Versions { get; init; } = [];

        /// <summary>
        /// True when the repository has more branches than this answer looked at, so unpublished versions
        /// may exist that are not listed. Reported rather than swallowed: a version list that is silently
        /// incomplete reads as "nothing else exists".
        /// </summary>
        public bool Truncated { get; init; }

        /// <summary>Cursor to pass back as <see cref="GitHistoryQuery.After"/> when <see cref="Truncated"/>.</summary>
        public string EndCursor { get; init; }
    }
}
