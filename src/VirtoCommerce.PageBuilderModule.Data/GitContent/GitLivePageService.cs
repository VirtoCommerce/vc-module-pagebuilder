using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.ContentModule.Core.Events;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

public class GitLivePageService(
    IGitContentRepository gitContentRepository,
    IGitContentHistory gitContentHistory,
    IGitHubConnectionService gitHubConnectionService,
    IContentPathResolver contentPathResolver,
    IBlobContentStorageProviderFactory blobContentStorageProviderFactory,
    IPublishingService publishingService,
    IEventPublisher eventPublisher)
    : IGitLivePageService
{
    private static readonly JsonSerializerSettings PageSerializerSettings = new() { Formatting = Formatting.Indented };

    public virtual async Task SyncLivePageAsync(string storeId, string contentType, string path, GitPageFileLocation location, bool removeWhenGone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        var baseBranch = await GetBaseBranchAsync(cancellationToken);
        // skip the read cache: GitHub may have merged since
        gitContentRepository.InvalidateRead(location.Repository, location.RepoPath, baseBranch);
        var published = await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, baseBranch, cancellationToken);

        if (published != null)
        {
            await WriteLivePageAsync(storeId, contentType, path, published, cancellationToken);
        }
        else if (removeWhenGone)
        {
            await RemoveLivePageAsync(storeId, contentType, path, cancellationToken);
        }
    }

    public virtual async Task<bool> IsLivePageBehindAsync(string storeId, string contentType, string path, GitPageFileLocation location, string publishedContent,
        CancellationToken cancellationToken = default)
    {
        var live = await GetStorageProvider(storeId, contentType).ReadTextAsync(GetLivePath(path));

        return publishedContent != null
            ? !PageJson.AreSameDocuments(live, publishedContent)
            : live != null && await IsRemovedFromBaseBranchAsync(location, cancellationToken);
    }

    public virtual async Task<bool> IsRemovedFromBaseBranchAsync(GitPageFileLocation location, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        var baseBranch = await GetBaseBranchAsync(cancellationToken);
        // read by head sha: a read by branch name may be cached from before the page was published again
        var head = await gitContentRepository.GetBranchHeadShaAsync(location.Repository, baseBranch, cancellationToken);
        if (head == null || await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, head, cancellationToken) != null)
        {
            return false;
        }

        var history = await gitContentHistory.GetHistoryAsync(location.Repository, location.RepoPath, new GitHistoryQuery { BaseBranch = baseBranch }, cancellationToken);
        return history.Versions.Any(x => x.Published);
    }

    protected virtual async Task WriteLivePageAsync(string storeId, string contentType, string path, string content, CancellationToken cancellationToken)
    {
        var type = contentType.ToLowerInvariant();
        var storageProvider = GetStorageProvider(storeId, type);

        // parsed before the live file is opened, which would empty it
        JToken page;
        try
        {
            page = JToken.Parse(content);
        }
        catch (JsonReaderException ex)
        {
            throw new GitContentException($"\"{path}\" on the base branch is not valid JSON, so the live page stays as it is. Fix the file in the repository.", ex);
        }

        await using (var stream = await storageProvider.OpenWriteAsync(GetLivePath(path)))
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(JsonConvert.SerializeObject(page, PageSerializerSettings));
        }

        await eventPublisher.Publish(new PageBuilderContentChangedEvent(type, [CreateChangedEntry(path, contentType, EntryState.Modified)]), cancellationToken);
        await PublishLiveFileChangedAsync(storeId, type, new GenericChangedEntry<ContentFile>(CreateLiveFile(path), null, EntryState.Modified), cancellationToken);
    }

    protected virtual async Task RemoveLivePageAsync(string storeId, string contentType, string path, CancellationToken cancellationToken)
    {
        await GetStorageProvider(storeId, contentType).RemoveAsync([GetLivePath(path)]);
        await eventPublisher.Publish(new PageBuilderContentChangedEvent(contentType, [CreateChangedEntry(path, contentType, EntryState.Deleted)]), cancellationToken);
        await PublishLiveFileChangedAsync(storeId, contentType.ToLowerInvariant(), new GenericChangedEntry<ContentFile>(null, CreateLiveFile(path), EntryState.Deleted), cancellationToken);
    }

    /// <summary>The live file was written past the Content module, whose full-text index follows this event.</summary>
    protected virtual Task PublishLiveFileChangedAsync(string storeId, string contentType, GenericChangedEntry<ContentFile> entry, CancellationToken cancellationToken) =>
        eventPublisher.Publish(new ContentFileChangedEvent(contentType, storeId, [entry]), cancellationToken);

    protected virtual ContentFile CreateLiveFile(string path)
    {
        var file = AbstractTypeFactory<ContentFile>.TryCreateInstance();
        file.RelativeUrl = GetLivePath(path);

        return file;
    }

    protected virtual IBlobContentStorageProvider GetStorageProvider(string storeId, string contentType) =>
        blobContentStorageProviderFactory.CreateProvider(contentPathResolver.GetContentBasePath(contentType, storeId));

    protected virtual string GetLivePath(string path) => publishingService.GetRelativeDraftUrl(path, false);

    protected virtual GenericChangedEntry<FileEntity> CreateChangedEntry(string path, string contentType, EntryState state)
    {
        var file = AbstractTypeFactory<FileEntity>.TryCreateInstance();
        file.Path = path;
        file.Type = contentType;

        return new GenericChangedEntry<FileEntity>(file, state);
    }

    private async Task<string> GetBaseBranchAsync(CancellationToken cancellationToken) =>
        (await gitHubConnectionService.GetConnectionAsync(cancellationToken)).BaseBranch;
}
