using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Web.Services;

internal sealed class PageBuilderPageContentService(
    IPageBuilderPageService pageService,
    IGroupedPageService groupedPageService,
    IPageBuilderLinkedComponentReferenceIndexService linkedComponentReferenceIndexService,
    IEventPublisher eventPublisher,
    ILogger logger)
{
    internal bool HasLinkedComponentReferences(string content)
    {
        return PageBuilderLinkedComponentReferenceMatcher.HasReferences(content);
    }

    internal Task ValidateReferencesForStoreAsync(
        string storeId,
        string content,
        CancellationToken cancellationToken)
    {
        return linkedComponentReferenceIndexService.ValidateReferencesForStoreAsync(
            storeId,
            [content],
            cancellationToken);
    }

    internal async Task<PageBuilderPageContentWriteResult> SaveGroupUpdateAsync(
        GroupedPageBuilderPage groupedPage,
        string sourcePageId,
        CancellationToken cancellationToken)
    {
        var draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);
        var createdDraftPageId = (string)null;

        if (draftPage == null)
        {
            draftPage = CreateDraft(groupedPage.StoreId);
            groupedPage.Pages.Add(draftPage);
            createdDraftPageId = draftPage.Id;
        }

        try
        {
            await groupedPageService.SaveChangesAsync([groupedPage]);
        }
        catch (InvalidDataException ex)
        {
            return PageBuilderPageContentWriteResult.Invalid(ex.Message);
        }

        if (sourcePageId != null)
        {
            var errorMessage = await TryWriteContentAsync(
                draftPage.Id,
                createdDraftPageId,
                () => groupedPageService.CopyPageContentAsync(
                    sourcePageId,
                    draftPage.Id,
                    cancellationToken));
            if (errorMessage != null)
            {
                return PageBuilderPageContentWriteResult.Invalid(errorMessage);
            }
        }

        await UpdateGroupSettingsAsync(draftPage.Id, groupedPage, cancellationToken);

        return PageBuilderPageContentWriteResult.Success;
    }

    internal async Task UpdateGroupSettingsAsync(
        string pageId,
        GroupedPageBuilderPage groupedPage,
        CancellationToken cancellationToken)
    {
        await SyncGroupSettingsToContentAsync(pageId, groupedPage, cancellationToken);
        await RaisePageContentChangedAsync(pageId, cancellationToken);
    }

    internal async Task<PageBuilderPageContentWriteResult> SaveContentAsync(
        string groupId,
        GroupedPageBuilderPage groupedPage,
        string content,
        CancellationToken cancellationToken)
    {
        var draft = await GetOrCreatePersistedDraftAsync(groupId, groupedPage);
        var pageId = draft.Page!.Id;
        var errorMessage = await TryWriteContentAsync(
            pageId,
            draft.CreatedPageId,
            () => groupedPageService.SaveContent(pageId, content, cancellationToken));

        if (errorMessage != null)
        {
            return PageBuilderPageContentWriteResult.Invalid(errorMessage);
        }

        await RaisePageContentChangedAsync(pageId, cancellationToken);

        return PageBuilderPageContentWriteResult.Success;
    }

    internal async Task<PageBuilderPageContentWriteResult> CopyContentAsync(
        string targetGroupId,
        GroupedPageBuilderPage targetGroup,
        string sourcePageId,
        CancellationToken cancellationToken)
    {
        var draft = await GetOrCreatePersistedDraftAsync(targetGroupId, targetGroup);
        if (draft.Page == null)
        {
            return PageBuilderPageContentWriteResult.NotFound;
        }

        var errorMessage = await TryWriteContentAsync(
            draft.Page.Id,
            draft.CreatedPageId,
            () => groupedPageService.CopyPageContentAsync(
                sourcePageId,
                draft.Page.Id,
                cancellationToken));
        if (errorMessage != null)
        {
            return PageBuilderPageContentWriteResult.Invalid(errorMessage);
        }

        await RaisePageContentChangedAsync(draft.Page.Id, cancellationToken);

        return PageBuilderPageContentWriteResult.Success;
    }

    private async Task<DraftPage> GetOrCreatePersistedDraftAsync(
        string groupId,
        GroupedPageBuilderPage groupedPage)
    {
        var draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);
        if (draftPage != null)
        {
            return new DraftPage(draftPage, null);
        }

        var createdDraftPage = CreateDraft(groupedPage.StoreId);
        groupedPage.Pages.Add(createdDraftPage);
        await groupedPageService.SaveChangesAsync([groupedPage]);

        groupedPage = await groupedPageService.GetByIdAsync(groupId);
        draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);

        return new DraftPage(draftPage, createdDraftPage.Id);
    }

    private static PageBuilderPage CreateDraft(string storeId)
    {
        var draftPage = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
        draftPage.Id = Guid.NewGuid().ToString("N");
        draftPage.StoreId = storeId;
        draftPage.Status = Draft;

        return draftPage;
    }

    private async Task<string> TryWriteContentAsync(
        string pageId,
        string createdDraftPageId,
        Func<Task> writeContent)
    {
        try
        {
            await writeContent();
            return null;
        }
        catch (Exception ex)
        {
            if (createdDraftPageId != null &&
                string.Equals(createdDraftPageId, pageId, StringComparison.OrdinalIgnoreCase))
            {
                await RemoveFailedDraftAsync(pageId, ex);
            }

            if (ex is InvalidDataException)
            {
                return ex.Message;
            }

            throw;
        }
    }

    private async Task RemoveFailedDraftAsync(string pageId, Exception originalException)
    {
        try
        {
            var deleted = await groupedPageService.TryDeleteEmptyDraftAsync(pageId);
            if (!deleted)
            {
                logger.LogDebug(
                    "Skipped cleanup of draft page {PageId} after a failed content write because it is no longer an empty draft",
                    pageId);
            }
        }
        catch (Exception cleanupException)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(
                    cleanupException,
                    "Failed to remove draft page {PageId} after its content write failed: {WriteError}",
                    pageId,
                    originalException.Message);
            }
        }
    }

    private async Task SyncGroupSettingsToContentAsync(
        string pageId,
        GroupedPageBuilderPage group,
        CancellationToken cancellationToken)
    {
        var content = await groupedPageService.LoadContent(pageId, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            content = ModuleConstants.DefaultPageContent;
        }

        var node = JsonNode.Parse(content);
        if (node is not JsonObject root)
        {
            return;
        }

        if (root["settings"] is not JsonObject settings)
        {
            settings = new JsonObject();
            root["settings"] = settings;
        }

        settings["name"] = group.Name;
        settings["permalink"] = group.Permalink;
        settings["cultureName"] = group.CultureName;

        await groupedPageService.SaveContent(pageId, root.ToJsonString(), cancellationToken);
    }

    private async Task RaisePageContentChangedAsync(string pageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pageId))
        {
            return;
        }

        var pages = await pageService.GetAsync([pageId]);
        var page = pages.FirstOrDefault();
        if (page == null)
        {
            return;
        }

        var entry = new GenericChangedEntry<PageBuilderPage>(page, EntryState.Modified);
        await eventPublisher.Publish(new PageBuilderPageChangedEvent([entry]), cancellationToken);
    }

    private sealed record DraftPage(PageBuilderPage Page, string CreatedPageId);
}

internal sealed record PageBuilderPageContentWriteResult(bool PageFound, string ErrorMessage)
{
    public static PageBuilderPageContentWriteResult Success { get; } = new(true, null);
    public static PageBuilderPageContentWriteResult NotFound { get; } = new(false, null);

    public static PageBuilderPageContentWriteResult Invalid(string errorMessage)
    {
        return new PageBuilderPageContentWriteResult(true, errorMessage);
    }
}
