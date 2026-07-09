using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// The module's gateway to the content repository. Git holds the pages, so this is where a draft
    /// lives (a commit on a work branch) and where the published state is read from (the production
    /// branch).
    /// <para>
    /// Writes are authoritative, not best-effort: when a commit fails the save that triggered it must
    /// fail too. A save that silently kept going would leave the editor believing their work is stored.
    /// </para>
    /// </summary>
    public interface IGitContentRepository
    {
        /// <summary>
        /// Contents of the file at <paramref name="gitRef"/> (a branch, tag or commit sha), or
        /// <c>null</c> when either the file or the ref does not exist — a page that was never created
        /// and a page on a branch that was never cut are the same answer to the caller.
        /// </summary>
        Task<string> ReadFileAsync(string path, string gitRef, CancellationToken cancellationToken = default);

        /// <summary>Commit sha the branch points at, or <c>null</c> when the branch does not exist.</summary>
        Task<string> GetBranchHeadShaAsync(string branch, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates <paramref name="branch"/> at <paramref name="fromRef"/>. Succeeds quietly when the
        /// branch already exists: two saves of the same page race only with each other, and both want
        /// the same branch to be there.
        /// </summary>
        Task CreateBranchAsync(string branch, string fromRef, CancellationToken cancellationToken = default);

        /// <summary>Deletes the branch. Succeeds quietly when it is already gone.</summary>
        Task DeleteBranchAsync(string branch, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates or updates the file on an existing branch. The branch is not created implicitly:
        /// a work branch must be cut from the production branch at a moment the caller chooses, so that
        /// what gets published is the page as it stands today plus this edit, and nothing else.
        /// </summary>
        /// <returns>The sha of the resulting commit.</returns>
        Task<string> CommitFileAsync(string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the file on the branch. Unpublishing a page is deleting it from the production
        /// branch, so this is the same operation as any other edit.
        /// </summary>
        /// <returns>The sha of the resulting commit.</returns>
        Task<string> DeleteFileAsync(string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default);
    }
}
