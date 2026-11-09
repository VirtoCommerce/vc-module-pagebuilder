using Microsoft.Extensions.Logging;
using VirtoCommerce.ContentModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemeDraftService(
    IThemeStorageFactory themeStorageFactory,
    IDistributedLock distributedLock,
    IEventPublisher eventPublisher,
    ILogger<ThemeDraftService> logger) : IThemeDraftService
{
    public virtual async Task<ThemeFileContent> ReadAsync(string storeId, string path, CancellationToken cancellationToken = default)
    {
        var result = AbstractTypeFactory<ThemeFileContent>.TryCreateInstance();
        result.Path = path;
        result.Content = await ReadEditedAsync(await ReadDraftAsync(storeId, path));
        result.ETag = PageBuilderContentVersion.CreateForThemeFile(storeId, path, result.Content);

        return result;
    }

    public virtual async Task<T> SaveDraftsAsync<T>(string storeId, IDictionary<string, string> files, IDictionary<string, string> eTags,
        Func<CancellationToken, Task<T>> saveOthersAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(saveOthersAsync);

        if (files.Count == 0)
        {
            // nothing of the theme is written, so the theme's writers need not wait
            return await saveOthersAsync(cancellationToken);
        }

        // a theme publication and the builder's other theme writes take the same lock: a version checked here stays current until written
        var result = await distributedLock.ExecuteAsync(
            ThemeStorage.GetLockResource(storeId),
            lockToken => SaveUnderLockAsync(storeId, files, eTags, saveOthersAsync, lockToken, cancellationToken),
            cancellationToken: cancellationToken);

        await PublishChangedEventAsync(files.Keys, cancellationToken);

        return result;
    }

    public virtual async Task<IList<DiscardedThemeDraft>> DiscardDraftsAsync(string storeId, IList<string> paths, Func<CancellationToken, Task> discardOthersAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(discardOthersAsync);

        if (paths.Count == 0)
        {
            await discardOthersAsync(cancellationToken);
            return [];
        }

        var result = await distributedLock.ExecuteAsync(
            ThemeStorage.GetLockResource(storeId),
            lockToken => DiscardUnderLockAsync(storeId, paths, discardOthersAsync, lockToken, cancellationToken),
            cancellationToken: cancellationToken);

        await PublishChangedEventAsync(result.Where(x => x.Status == DraftDiscardStatus.Discarded).Select(x => x.Path), cancellationToken);

        return result;
    }

    protected virtual async Task<T> SaveUnderLockAsync<T>(string storeId, IDictionary<string, string> files, IDictionary<string, string> eTags,
        Func<CancellationToken, Task<T>> saveOthersAsync, CancellationToken cancellationToken, CancellationToken requestToken)
    {
        // every version is checked before anything is written
        var drafts = new List<(ThemeDraft Draft, string Content)>();
        foreach (var (path, content) in files)
        {
            var draft = await ReadDraftAsync(storeId, path);
            if (eTags != null)
            {
                await CheckVersionAsync(storeId, path, draft, content, eTags.TryGetValue(path, out var eTag) ? eTag : null);
            }

            drafts.Add((draft, content));
        }

        var previousDrafts = new List<ThemeDraft>();
        try
        {
            foreach (var (draft, content) in drafts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                previousDrafts.Add(draft);
                await draft.Storage.WriteAsync(draft.File, content);
            }

            return await saveOthersAsync(cancellationToken);
        }
        catch
        {
            // with the lock lost the drafts may be another server's by now
            if (!ThemeStorage.IsLockLost(cancellationToken, requestToken))
            {
                await RestoreDraftsAsync(storeId, previousDrafts);
            }

            throw;
        }
    }

    protected virtual async Task<IList<DiscardedThemeDraft>> DiscardUnderLockAsync(string storeId, IList<string> paths, Func<CancellationToken, Task> discardOthersAsync,
        CancellationToken cancellationToken, CancellationToken requestToken)
    {
        var removedDrafts = new List<ThemeDraft>();
        var result = new List<DiscardedThemeDraft>();
        try
        {
            foreach (var path in paths)
            {
                var draft = await ReadDraftAsync(storeId, path);
                var discarded = AbstractTypeFactory<DiscardedThemeDraft>.TryCreateInstance();
                discarded.Path = path;
                discarded.PublishedContent = draft.Content == null ? null : await draft.Storage.ReadAsync(draft.Storage.GetLivePath(draft.File));
                discarded.Status = GetDiscardStatus(draft, discarded.PublishedContent);
                result.Add(discarded);

                if (discarded.Status == DraftDiscardStatus.Discarded)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await draft.Storage.RemoveAsync([draft.File]);
                    removedDrafts.Add(draft);
                }
            }

            await discardOthersAsync(cancellationToken);

            return result;
        }
        catch
        {
            // with the lock lost the drafts may be another server's by now
            if (!ThemeStorage.IsLockLost(cancellationToken, requestToken))
            {
                await RestoreDraftsAsync(storeId, removedDrafts);
            }

            throw;
        }
    }

    protected virtual async Task<ThemeDraft> ReadDraftAsync(string storeId, string path)
    {
        var storage = themeStorageFactory.Open(storeId, PageBuilderContentPaths.GetThemeName(path));
        var file = storage.GetDraftPath(PageBuilderContentPaths.GetPathInTheme(path));

        return new ThemeDraft(storage, file, await storage.ReadAsync(file));
    }

    protected virtual async Task<string> ReadEditedAsync(ThemeDraft draft) =>
        draft.Content ?? await draft.Storage.ReadAsync(draft.Storage.GetLivePath(draft.File));

    /// <summary>The content sent being there already is a retried save, and passes.</summary>
    protected virtual async Task CheckVersionAsync(string storeId, string path, ThemeDraft draft, string content, string eTag)
    {
        var current = await ReadEditedAsync(draft);
        if (!string.Equals(PageBuilderContentVersion.CreateForThemeFile(storeId, path, current), eTag, StringComparison.Ordinal) &&
            !string.Equals(current, content, StringComparison.Ordinal))
        {
            throw new PageBuilderContentConflictException(PageBuilderContentConflictException.ThemeFileMessage);
        }
    }

    /// <summary>A draft of a file never published is its only copy and stays.</summary>
    protected virtual DraftDiscardStatus GetDiscardStatus(ThemeDraft draft, string publishedContent)
    {
        if (draft.Content == null)
        {
            return DraftDiscardStatus.NoDraft;
        }

        return publishedContent == null ? DraftDiscardStatus.OnlyCopy : DraftDiscardStatus.Discarded;
    }

    protected virtual async Task RestoreDraftsAsync(string storeId, IList<ThemeDraft> drafts)
    {
        foreach (var draft in drafts.Reverse())
        {
            try
            {
                if (draft.Content == null)
                {
                    await draft.Storage.RemoveAsync([draft.File]);
                }
                else
                {
                    await draft.Storage.WriteAsync(draft.File, draft.Content);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not put back the draft {File} of theme {Theme} (store {StoreId}) after a failed save or discard.",
                    draft.File, draft.Storage.ThemeName, storeId);
            }
        }
    }

    protected virtual async Task PublishChangedEventAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        var entries = paths.Select(CreateChangedEntry).ToList();
        if (entries.Count > 0)
        {
            await eventPublisher.Publish(new PageBuilderContentChangedEvent(ContentConstants.ContentTypes.Themes, entries), cancellationToken);
        }
    }

    protected virtual GenericChangedEntry<FileEntity> CreateChangedEntry(string path)
    {
        var file = AbstractTypeFactory<FileEntity>.TryCreateInstance();
        file.Path = path;
        file.Type = ContentConstants.ContentTypes.Themes;

        return new GenericChangedEntry<FileEntity>(file, EntryState.Modified);
    }

    protected sealed record ThemeDraft(IThemeStorage Storage, string File, string Content);
}
