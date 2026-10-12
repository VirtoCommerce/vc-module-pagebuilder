using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Web.Services;

[method: ActivatorUtilitiesConstructor]
public sealed class PageBuilderPageContentService(
    IPageBuilderPageService pageService,
    IGroupedPageService groupedPageService,
    IPageBuilderSharedComponentReferenceIndexService sharedComponentReferenceIndexService,
    IEventPublisher eventPublisher,
    ILogger<PageBuilderPageContentService> logger,
    IGroupedPageContentService versionedContent)
{
    public PageBuilderPageContentService(
        IPageBuilderPageService pageService,
        IGroupedPageService groupedPageService,
        IPageBuilderSharedComponentReferenceIndexService sharedComponentReferenceIndexService,
        IEventPublisher eventPublisher,
        ILogger<PageBuilderPageContentService> logger)
        : this(pageService, groupedPageService, sharedComponentReferenceIndexService, eventPublisher, logger,
            groupedPageService as IGroupedPageContentService)
    {
    }

    public bool HasSharedComponentReferences(string content)
    {
        return PageBuilderSharedComponentReferenceMatcher.HasReferences(content);
    }

    public Task ValidateReferencesForStoreAsync(
        string storeId,
        string content,
        CancellationToken cancellationToken)
    {
        return sharedComponentReferenceIndexService.ValidateReferencesForStoreAsync(
            storeId,
            [content],
            cancellationToken);
    }

    private IGroupedPageContentService VersionedContent => versionedContent
        ?? throw new InvalidOperationException("Supply IGroupedPageContentService when constructing a custom content service.");

    public Task<PageBuilderContentSnapshot> LoadGroupContentAsync(
        GroupedPageBuilderPage group, bool draft, CancellationToken cancellationToken)
        => VersionedContent.LoadGroupContentAsync(group, draft, cancellationToken);

    /// <summary>The source page is selected from current membership under the group lock; sourcePageId is retained for compatibility.</summary>
    public async Task<PageBuilderPageContentWriteResult> SaveGroupUpdateAsync(
        GroupedPageBuilderPage groupedPage,
        string sourcePageId,
        CancellationToken cancellationToken)
    {
        try
        {
            // The service reads the authoritative source under the group lock; a previously selected
            // sourcePageId can be stale by the time a Shell settings update reaches persistence.
            await UpdateGroupSettingsAsync(null, groupedPage, cancellationToken);
            return PageBuilderPageContentWriteResult.Success;
        }
        catch (InvalidDataException ex)
        {
            return PageBuilderPageContentWriteResult.Invalid(ex.Message);
        }
    }

    /// <summary>The draft is selected under the group lock; pageId is retained for compatibility.</summary>
    public async Task UpdateGroupSettingsAsync(
        string pageId,
        GroupedPageBuilderPage groupedPage,
        CancellationToken cancellationToken)
    {
        await VersionedContent.SaveGroupSettingsAsync(groupedPage, cancellationToken);
    }

    public async Task<PageBuilderPageContentWriteResult> SaveContentAsync(
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

    public async Task<PageBuilderConditionalContentWriteResult> SaveConditionalContentAsync(
        GroupedPageBuilderPage group, string content, string expectedETag, CancellationToken cancellationToken)
    {
        var result = await VersionedContent.SaveGroupContentAsync(group, content, expectedETag, cancellationToken);
        if (result.ContentWritten && !result.GroupedEventsPublished)
        {
            await RaisePageContentChangedAsync(result.PageId, cancellationToken);
        }
        return result;
    }

    public async Task<PageBuilderPageContentWriteResult> CopyContentAsync(
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
        var draftPage = PageBuilderPageSelection.Order(groupedPage.Pages).FirstOrDefault(x => x.Status == Draft);
        if (draftPage != null)
        {
            return new DraftPage(draftPage, null);
        }

        var createdDraftPage = CreateDraft(groupedPage.StoreId);
        groupedPage.Pages.Add(createdDraftPage);
        await groupedPageService.SaveChangesAsync([groupedPage]);

        groupedPage = await groupedPageService.GetByIdAsync(groupId);
        draftPage = PageBuilderPageSelection.Order(groupedPage.Pages).FirstOrDefault(x => x.Status == Draft);

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
            if (!deleted && logger.IsEnabled(LogLevel.Debug))
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

public sealed record PageBuilderPageContentWriteResult(bool PageFound, string ErrorMessage)
{
    public static PageBuilderPageContentWriteResult Success { get; } = new(true, null);
    public static PageBuilderPageContentWriteResult NotFound { get; } = new(false, null);

    public static PageBuilderPageContentWriteResult Invalid(string errorMessage)
    {
        return new PageBuilderPageContentWriteResult(true, errorMessage);
    }
}
