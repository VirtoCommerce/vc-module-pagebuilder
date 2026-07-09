namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Writes content files into the git repository (the round-trip of designer edits back to git).
    /// The commit is an authoritative step of the save flow, not best-effort: a failure must fail
    /// the whole save, otherwise git and the blob draft drift apart.
    /// </summary>
    public interface IGitContentWriter
    {
        /// <summary>
        /// Commits the file at the repository-relative path to the given branch, creating the
        /// branch from the configured base branch when it does not exist yet, and creating or
        /// updating the file as needed.
        /// </summary>
        Task CommitFileAsync(string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default);
    }
}
