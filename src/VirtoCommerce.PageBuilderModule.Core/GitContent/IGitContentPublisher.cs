using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Publishing a page is merging its work branch into the production branch. Unpublishing is the
    /// same operation over a branch whose commit deletes the file, which is why there is one method
    /// here and not two: the difference lives in the commit, not in the act of shipping it.
    /// </summary>
    public interface IGitContentPublisher
    {
        /// <summary>
        /// Opens a pull request from <paramref name="branch"/> into the production branch (reusing the
        /// open one if the editor already asked), and lets it merge as soon as the required checks pass.
        /// Never merges past a failing check, and never resolves a conflict on the editor's behalf.
        /// </summary>
        Task<GitPublishResult> MergeBranchAsync(string branch, string title, CancellationToken cancellationToken = default);

        /// <summary>
        /// Number of the open pull request from <paramref name="branch"/>, or <c>null</c> when there is
        /// none. A page with one is on its way to production and must not be reported as published yet.
        /// </summary>
        Task<int?> GetOpenPullRequestNumberAsync(string branch, CancellationToken cancellationToken = default);
    }
}
