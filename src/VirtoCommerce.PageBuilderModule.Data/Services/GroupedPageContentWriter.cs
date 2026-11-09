using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

/// <summary>Raises the page's changed event when the content service did not.</summary>
public class GroupedPageContentWriter(
    IGroupedPageContentService groupedPageContentService,
    IPageBuilderPageService pageService,
    IEventPublisher eventPublisher) : IGroupedPageContentWriter
{
    public virtual async Task<PageBuilderConditionalContentWriteResult> SaveContentAsync(GroupedPageBuilderPage group, string content, string expectedETag,
        CancellationToken cancellationToken = default)
    {
        var result = await groupedPageContentService.SaveGroupContentAsync(group, content, expectedETag, cancellationToken);
        if (result.ContentWritten && !result.GroupedEventsPublished)
        {
            await PublishPageChangedEventAsync(result.PageId, cancellationToken);
        }

        return result;
    }

    protected virtual async Task PublishPageChangedEventAsync(string pageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pageId))
        {
            return;
        }

        var page = (await pageService.GetAsync([pageId])).FirstOrDefault();
        if (page == null)
        {
            return;
        }

        await eventPublisher.Publish(new PageBuilderPageChangedEvent([new GenericChangedEntry<PageBuilderPage>(page, EntryState.Modified)]), cancellationToken);
    }
}
