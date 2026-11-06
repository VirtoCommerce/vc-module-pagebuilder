using Microsoft.Extensions.Logging;
using VirtoCommerce.ContentModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;
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
    public virtual async Task<T> SaveDraftsAsync<T>(string storeId, IDictionary<string, string> files, Func<CancellationToken, Task<T>> saveOthersAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(saveOthersAsync);

        // a theme publication reads and deletes the drafts under the same lock
        var result = await distributedLock.ExecuteAsync(
            ThemeStorage.GetLockResource(storeId),
            _ => SaveUnderLockAsync(storeId, files, saveOthersAsync, cancellationToken),
            cancellationToken: cancellationToken);

        if (files.Count > 0)
        {
            var entries = files.Keys.Select(CreateChangedEntry).ToList();
            await eventPublisher.Publish(new PageBuilderContentChangedEvent(ContentConstants.ContentTypes.Themes, entries), cancellationToken);
        }

        return result;
    }

    protected virtual async Task<T> SaveUnderLockAsync<T>(string storeId, IDictionary<string, string> files, Func<CancellationToken, Task<T>> saveOthersAsync,
        CancellationToken cancellationToken)
    {
        var previousDrafts = new List<ThemeDraft>();
        try
        {
            foreach (var (path, content) in files)
            {
                var draft = await ReadDraftAsync(storeId, path);
                previousDrafts.Add(draft);
                await draft.Storage.WriteAsync(draft.File, content);
            }

            return await saveOthersAsync(cancellationToken);
        }
        catch
        {
            await RestoreDraftsAsync(storeId, previousDrafts);
            throw;
        }
    }

    protected virtual async Task<ThemeDraft> ReadDraftAsync(string storeId, string path)
    {
        var separator = path.IndexOf('/');
        var storage = themeStorageFactory.Open(storeId, path[..separator]);
        var file = storage.GetDraftPath(path[(separator + 1)..]);

        return new ThemeDraft(storage, file, await storage.ReadAsync(file));
    }

    /// <summary>Puts the drafts back as they were, the last written first.</summary>
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
                logger.LogError(ex, "Could not put back the draft {File} of theme {Theme} (store {StoreId}) after a failed save.",
                    draft.File, draft.Storage.ThemeName, storeId);
            }
        }
    }

    protected virtual GenericChangedEntry<FileEntity> CreateChangedEntry(string path)
    {
        var file = AbstractTypeFactory<FileEntity>.TryCreateInstance();
        file.Path = path;
        file.Type = ContentConstants.ContentTypes.Themes;

        return new GenericChangedEntry<FileEntity>(file, EntryState.Modified);
    }

    /// <summary>A theme draft as it was before the save.</summary>
    protected sealed record ThemeDraft(IThemeStorage Storage, string File, string Content);
}
