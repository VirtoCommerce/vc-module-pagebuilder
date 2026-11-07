namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Live storage pages of a repository the platform manages, kept in line with its base branch.</summary>
public interface IGitLivePageService
{
    /// <summary>
    /// Writes the page from the base branch to live storage. A page missing from the branch is removed only with
    /// <paramref name="removeWhenGone"/>.
    /// </summary>
    Task SyncLivePageAsync(string storeId, string contentType, string path, GitPageFileLocation location, bool removeWhenGone,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether the live page differs from <paramref name="publishedContent"/>, the page on the base branch.</summary>
    Task<bool> IsLivePageBehindAsync(string storeId, string contentType, string path, GitPageFileLocation location, string publishedContent,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether the page was on the base branch and is not anymore.</summary>
    Task<bool> IsRemovedFromBaseBranchAsync(GitPageFileLocation location, CancellationToken cancellationToken = default);
}
