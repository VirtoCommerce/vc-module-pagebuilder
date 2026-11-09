using System.Security.Claims;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class DesignerContentService(
    IThemeDraftService themeDraftService,
    IThemePublisher themePublisher,
    IThemeSnapshotService themeSnapshotService,
    IPageBuilderPageService pageService,
    IGroupedPageService groupedPageService,
    IGroupedPageContentService groupedPageContentService,
    IGroupedPageContentWriter groupedPageContentWriter,
    IFilePageDraftService filePageDraftService,
    IContentHistory contentHistory) : IDesignerContentService
{
    public virtual async Task ValidateThemeFilesAsync(string storeId, IDictionary<string, JToken> files, bool draft, string catalogHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        // the new Page Builder publishes the theme as a whole, from drafts
        if (!draft && await themeSnapshotService.IsNewPageBuilderEnabledAsync(storeId, cancellationToken))
        {
            throw new InvalidDataException("The store uses the new Page Builder: theme files are saved as drafts and published with theme/publish.");
        }

        if (string.IsNullOrEmpty(catalogHash))
        {
            return;
        }

        // each theme folder on its own: two themes may hold a file of the same name
        foreach (var theme in files.GroupBy(x => PageBuilderContentPaths.GetThemeName(x.Key), StringComparer.OrdinalIgnoreCase))
        {
            var contents = theme.ToDictionary(x => PageBuilderContentPaths.GetPathInTheme(x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);
            await themePublisher.ValidateFilesAsync(storeId, catalogHash, contents, cancellationToken);
        }
    }

    public virtual async Task<DesignerSaveResult> SaveAsync(DesignerSaveCommand command, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = AbstractTypeFactory<DesignerSaveResult>.TryCreateInstance();
        result.SaveId = command.SaveId;
        result.ETag = await themeDraftService.SaveDraftsAsync(
            command.StoreId,
            command.ThemeFiles.ToDictionary(x => x.Path, x => x.Content),
            command.ThemeFiles.ToDictionary(x => x.Path, x => x.ETag),
            token => SavePageAsync(command, user, token),
            cancellationToken);
        result.ThemeFiles = command.ThemeFiles.Select(x => CreateThemeFileVersion(command.StoreId, x)).ToList();

        var record = CreateHistoryRecord(ModuleConstants.GitHistory.SavedSummary, command.SaveId, user);
        if (command.Group != null)
        {
            record.AddEntry(new HistoryItem(HistoryItemType.Page, command.Group.Id), command.PageContent);
        }

        foreach (var file in command.ThemeFiles)
        {
            record.AddEntry(HistoryItem.ForThemeFile(file.Path), file.Content);
        }

        await contentHistory.RecordAsync(command.StoreId, record, cancellationToken);

        return result;
    }

    public virtual async Task<ContentSaveResult> DiscardAsync(DesignerDiscardCommand command, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        string publishedPageContent = null;
        var themeDrafts = await themeDraftService.DiscardDraftsAsync(
            command.StoreId,
            command.ThemeFiles,
            async token => publishedPageContent = await DiscardPageAsync(command, user, token),
            cancellationToken);

        var record = CreateHistoryRecord(ModuleConstants.GitHistory.DiscardedSummary, command.SaveId, user);
        if (publishedPageContent != null)
        {
            record.AddEntry(new HistoryItem(HistoryItemType.Page, command.Group.Id), publishedPageContent);
        }

        foreach (var draft in themeDrafts.Where(x => x.Status == DraftDiscardStatus.Discarded))
        {
            record.AddEntry(HistoryItem.ForThemeFile(draft.Path), draft.PublishedContent);
        }

        if (record.Entries.Count > 0)
        {
            await contentHistory.RecordAsync(command.StoreId, record, cancellationToken);
        }

        var result = AbstractTypeFactory<ContentSaveResult>.TryCreateInstance();
        result.SaveId = command.SaveId;

        return result;
    }

    public virtual async Task<ContentSaveResult> RestoreAsync(ContentRestoreCommand command, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var themeFiles = command.Contents.Where(x => x.Key.Type == HistoryItemType.Theme).ToDictionary(x => x.Key.Key, x => x.Value);
        var pageContent = command.Contents.Where(x => x.Key.Type == HistoryItemType.Page).Select(x => x.Value).SingleOrDefault();
        // a version restored replaces the drafts as they are now, as the page's restore does
        await themeDraftService.SaveDraftsAsync(command.StoreId, themeFiles, eTags: null, token => RestorePageAsync(command.Group, pageContent, token), cancellationToken);

        var record = CreateHistoryRecord(GetRestoredDescription(command.VersionId), command.SaveId, user);
        foreach (var (item, content) in command.Contents)
        {
            record.AddEntry(item, content);
        }

        await contentHistory.RecordAsync(command.StoreId, record, cancellationToken);

        var result = AbstractTypeFactory<ContentSaveResult>.TryCreateInstance();
        result.SaveId = command.SaveId;

        return result;
    }

    /// <summary>Writes the page last, so that a refused page leaves the theme drafts to be put back.</summary>
    protected virtual async Task<string> SavePageAsync(DesignerSaveCommand command, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (command.Group != null)
        {
            var written = await groupedPageContentWriter.SaveContentAsync(command.Group, command.PageContent, command.PageETag, cancellationToken);
            return written.ETag;
        }

        if (command.FilePage != null)
        {
            await filePageDraftService.SaveDraftAsync(command.StoreId, command.FilePage, user, cancellationToken);
        }

        return null;
    }

    /// <summary>Discards the page's drafts last, so that a refused page leaves the theme drafts to be put back.</summary>
    protected virtual async Task<string> DiscardPageAsync(DesignerDiscardCommand command, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (command.Group != null)
        {
            return await DiscardPageDraftsAsync(command.Group, cancellationToken);
        }

        if (command.FilePage != null)
        {
            await filePageDraftService.DiscardDraftAsync(command.StoreId, command.FilePage.ContentType, command.FilePage.Path, user, cancellationToken);
        }

        return null;
    }

    /// <summary>Deletes the drafts of a published database page. Returns the published content, or null when the page had no draft.</summary>
    /// <exception cref="InvalidDataException">The page has never been published.</exception>
    protected virtual async Task<string> DiscardPageDraftsAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken)
    {
        var publishedPage = PageBuilderPageSelection.Order(group.Pages).FirstOrDefault(x => x.Status == Published)
            ?? throw new InvalidDataException("This page has never been published, so there is no published version to go back to.");

        var draftIds = group.Pages.Where(x => x.Status == Draft).Select(x => x.Id).ToList();
        if (draftIds.Count == 0)
        {
            return null;
        }

        // read first: the history entry needs it, and nothing may fail once the drafts are gone
        var publishedContent = await groupedPageService.LoadContent(publishedPage.Id, cancellationToken);
        await pageService.DeleteAsync(draftIds);

        return publishedContent;
    }

    protected virtual async Task<bool> RestorePageAsync(GroupedPageBuilderPage group, string content, CancellationToken cancellationToken)
    {
        if (group == null)
        {
            return false;
        }

        var current = await groupedPageContentService.LoadGroupContentAsync(group, draft: true, cancellationToken);
        await groupedPageContentWriter.SaveContentAsync(group, content, current.ETag, cancellationToken);

        return true;
    }

    protected virtual ThemeFileVersion CreateThemeFileVersion(string storeId, DesignerSaveFile file)
    {
        var version = AbstractTypeFactory<ThemeFileVersion>.TryCreateInstance();
        version.Path = file.Path;
        version.ETag = PageBuilderContentVersion.CreateForThemeFile(storeId, file.Path, file.Content);

        return version;
    }

    protected virtual string GetRestoredDescription(string versionId) =>
        $"{ModuleConstants.GitHistory.RestoredSummary} {versionId[..Math.Min(ModuleConstants.GitHistory.RestoredVersionLength, versionId.Length)]}";

    protected virtual ContentHistoryRecord CreateHistoryRecord(string description, string saveId, ClaimsPrincipal user) =>
        ContentHistoryRecord.Create(description, saveId, user);
}
