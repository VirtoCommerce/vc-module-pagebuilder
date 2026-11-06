using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.ContentModule.Core;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>Version history of a theme file in the store repository.</summary>
public class ThemeFileHistoryService(
    IGitContentPolicy gitContentPolicy,
    IGitContentRepository gitContentRepository,
    IGitHubApiClient gitHubApiClient,
    IGitHubConnectionService gitHubConnectionService,
    IThemeStorageFactory themeStorageFactory,
    IDistributedLock distributedLock,
    IEventPublisher eventPublisher,
    IOptions<GitContentOptions> options,
    ILogger<ThemeFileHistoryService> logger)
    : GitHubFileHistoryService(gitContentPolicy, gitContentRepository, gitHubApiClient, gitHubConnectionService, options, logger), IThemeFileHistoryService
{
    public const string Folder = "theme";

    public virtual Task RecordAsync(string storeId, string path, string content, string summary, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default)
    {
        return PageBuilderContentPaths.IsThemeFilePath(path)
            ? RecordFileAsync(storeId, GetFilePath(path), GetCommitSubject(path), content, summary, user, saveId, cancellationToken)
            : Task.CompletedTask;
    }

    public virtual async Task<GitFileHistoryResult> GetHistoryAsync(string storeId, string path, string after = null, int take = ModuleConstants.GitHistory.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var publishedContent = await GetPublishedContentAsync(storeId, path, cancellationToken);
        return await GetFileHistoryAsync(storeId, GetFilePath(path), GetCommitSubject(path), publishedContent, after, take, cancellationToken);
    }

    public virtual Task<string> ReadContentAsync(string storeId, string path, string sha, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return ReadFileAsync(storeId, GetFilePath(path), sha, cancellationToken);
    }

    public virtual Task<string> GetPublishedContentAsync(string storeId, string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var storage = OpenThemeStorage(storeId, path);
        return storage.ReadAsync(storage.GetLivePath(GetThemeFile(path)));
    }

    public virtual async Task<bool> RestoreAsync(string storeId, string path, string sha, string version, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default)
    {
        var content = await ReadContentAsync(storeId, path, sha, cancellationToken);
        if (content == null)
        {
            return false;
        }

        await WriteDraftAsync(storeId, path, content, cancellationToken);
        await eventPublisher.Publish(new PageBuilderContentChangedEvent(ContentConstants.ContentTypes.Themes, [CreateChangedEntry(path)]), cancellationToken);
        await RecordAsync(storeId, path, content, GetRestoredSummary(version, sha), user, saveId, cancellationToken);

        return true;
    }

    /// <summary>Writes the content as it is: the published change is told by its bytes.</summary>
    protected virtual Task WriteDraftAsync(string storeId, string path, string content, CancellationToken cancellationToken)
    {
        var storage = OpenThemeStorage(storeId, path);

        // a theme publication deletes the drafts it puts live
        return distributedLock.ExecuteAsync(ThemeStorage.GetLockResource(storeId),
            _ => storage.WriteAsync(storage.GetDraftPath(GetThemeFile(path)), content),
            cancellationToken: cancellationToken);
    }

    protected virtual IThemeStorage OpenThemeStorage(string storeId, string path) => themeStorageFactory.Open(storeId, path.Split('/')[0]);

    protected virtual string GetThemeFile(string path) => path[(path.IndexOf('/') + 1)..];

    protected virtual GenericChangedEntry<FileEntity> CreateChangedEntry(string path)
    {
        var file = AbstractTypeFactory<FileEntity>.TryCreateInstance();
        file.Path = path;
        file.Type = ContentConstants.ContentTypes.Themes;

        return new GenericChangedEntry<FileEntity>(file, EntryState.Modified);
    }

    protected virtual string GetFilePath(string path) => $"{Folder}/{path}";

    protected virtual string GetCommitSubject(string path) => $": {path}";
}
