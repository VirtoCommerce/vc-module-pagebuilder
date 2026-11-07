using System.Security.Claims;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class FilePageDraftService(
    IGitContentPolicy gitContentPolicy,
    IGitContentRepository gitContentRepository,
    IGitContentHistory gitContentHistory,
    IGitContentProvisioner gitContentProvisioner,
    IGitHubConnectionService gitHubConnectionService,
    IOptions<GitContentOptions> gitContentOptions,
    IThemeStorageFactory themeStorageFactory,
    IContentPathResolver contentPathResolver,
    IBlobContentStorageProviderFactory blobContentStorageProviderFactory,
    IPublishingService publishingService,
    IEventPublisher eventPublisher) : IFilePageDraftService
{
    private const string SaveAction = "save";

    public virtual async Task SaveDraftAsync(string storeId, PageBuilderContentFile page, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (await gitContentPolicy.IsEnabledForStoreAsync(storeId, cancellationToken))
        {
            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, cancellationToken);
            await CommitDraftAsync(storeId, repository, page, SaveAction, user, cancellationToken);
            return;
        }

        await SaveBlobDraftAsync(storeId, page, cancellationToken);
    }

    public virtual async Task<GitPageCommit> CommitDraftAsync(string storeId, string repository, PageBuilderContentFile page, string action, ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        var options = gitContentOptions.Value;
        var baseBranch = (await gitHubConnectionService.GetConnectionAsync(cancellationToken)).BaseBranch;
        var location = options.GetPageLocation(repository, page.ContentType, page.Path, user?.Identity?.Name);

        // cut from the base branch at the first save, so a merge ships today's published page plus this edit only
        if (await gitContentRepository.GetBranchHeadShaAsync(repository, location.Branch, cancellationToken) == null)
        {
            // the store was enabled before its repository was created
            if (await gitContentRepository.GetBranchHeadShaAsync(repository, baseBranch, cancellationToken) == null)
            {
                await gitContentProvisioner.EnsureRepositoryAsync(storeId, cancellationToken);
            }

            await gitContentRepository.CreateBranchAsync(repository, location.Branch, baseBranch, cancellationToken);
        }

        var author = options.GetCommitAuthor(user);
        var message = GitCommitMessage.WithVcUser($"designer: {action} {page.Path} (store: {storeId}, by: {author.Name})", user?.Identity?.Name);

        // canonical bytes: publish status compares this branch with the base branch
        var commitSha = await gitContentRepository.CommitFileAsync(repository, location.RepoPath, PageJson.Serialize(page.Content), location.Branch,
            message, author, cancellationToken);
        gitContentHistory.Invalidate(repository, location.RepoPath);

        var result = AbstractTypeFactory<GitPageCommit>.TryCreateInstance();
        result.Path = page.Path;
        result.Branch = location.Branch;
        result.CommitSha = commitSha;

        return result;
    }

    public virtual async Task<DraftDiscardStatus> DiscardDraftAsync(string storeId, string contentType, string path, ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, cancellationToken))
        {
            return await DiscardBlobDraftAsync(storeId, contentType, path, theme: null, cancellationToken);
        }

        var repository = await gitContentPolicy.GetRepositoryAsync(storeId, cancellationToken);
        await DiscardGitDraftAsync(repository, contentType, path, user, cancellationToken);

        return DraftDiscardStatus.Discarded;
    }

    public virtual async Task DiscardGitDraftAsync(string repository, string contentType, string path, ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var location = gitContentOptions.Value.GetPageLocation(repository, contentType, path, user?.Identity?.Name);

        await gitContentRepository.DeleteBranchAsync(location.Repository, location.Branch, location.RepoPath, cancellationToken);
        // the versions that lived only on that branch are gone with it
        gitContentHistory.Invalidate(location.Repository, location.RepoPath);
    }

    public virtual async Task<DraftDiscardStatus> DiscardBlobDraftAsync(string storeId, string contentType, string path, string theme,
        CancellationToken cancellationToken = default)
    {
        var storageProvider = await CreateStorageProviderAsync(storeId, contentType, theme);
        var draftPath = publishingService.GetRelativeDraftUrl(path, true);
        if (await storageProvider.GetBlobInfoAsync(draftPath) == null)
        {
            return DraftDiscardStatus.NoDraft;
        }

        if (await storageProvider.GetBlobInfoAsync(publishingService.GetRelativeDraftUrl(path, false)) == null)
        {
            return DraftDiscardStatus.OnlyCopy;
        }

        await storageProvider.RemoveAsync([draftPath]);
        await PublishChangedEventAsync(contentType, path, cancellationToken);

        return DraftDiscardStatus.Discarded;
    }

    protected virtual async Task SaveBlobDraftAsync(string storeId, PageBuilderContentFile page, CancellationToken cancellationToken)
    {
        var contentType = page.ContentType.ToLowerInvariant();
        var storageProvider = await CreateStorageProviderAsync(storeId, contentType, theme: null);

        await using (var stream = await storageProvider.OpenWriteAsync(publishingService.GetRelativeDraftUrl(page.Path, true)))
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(JsonConvert.SerializeObject(page.Content, Formatting.Indented));
        }

        await PublishChangedEventAsync(contentType, page.Path, cancellationToken);
    }

    protected virtual async Task<IBlobContentStorageProvider> CreateStorageProviderAsync(string storeId, string contentType, string theme)
    {
        var themeName = string.IsNullOrEmpty(theme) ? await themeStorageFactory.GetThemeNameAsync(storeId) : theme;

        return blobContentStorageProviderFactory.CreateProvider(contentPathResolver.GetContentBasePath(contentType, storeId, themeName));
    }

    protected virtual Task PublishChangedEventAsync(string contentType, string path, CancellationToken cancellationToken)
    {
        var file = AbstractTypeFactory<FileEntity>.TryCreateInstance();
        file.Path = path;
        file.Type = contentType;

        return eventPublisher.Publish(new PageBuilderContentChangedEvent(contentType, [new GenericChangedEntry<FileEntity>(file, EntryState.Modified)]),
            cancellationToken);
    }
}
