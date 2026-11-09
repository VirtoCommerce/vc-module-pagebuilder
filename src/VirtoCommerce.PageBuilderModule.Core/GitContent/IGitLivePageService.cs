namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public interface IGitLivePageService
{
    Task SyncLivePageAsync(string storeId, string contentType, string path, GitPageFileLocation location, bool removeWhenGone,
        CancellationToken cancellationToken = default);

    Task<bool> IsLivePageBehindAsync(string storeId, string contentType, string path, GitPageFileLocation location, string publishedContent,
        CancellationToken cancellationToken = default);

    Task<bool> IsRemovedFromBaseBranchAsync(GitPageFileLocation location, CancellationToken cancellationToken = default);
}
