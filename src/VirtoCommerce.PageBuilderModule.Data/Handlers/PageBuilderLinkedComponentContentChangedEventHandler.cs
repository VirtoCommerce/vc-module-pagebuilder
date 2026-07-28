using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public class PageBuilderLinkedComponentContentChangedEventHandler(
    IPageBuilderLinkedComponentReferenceIndexService referenceIndexService,
    IPageBuilderPageService pageService,
    IGroupedPageService groupedPageService,
    IPageBuilderLinkedComponentResolver resolver,
    IPageBuilderAssetReferenceIndexService assetReferenceIndexService,
    IEventPublisher eventPublisher)
    : IEventHandler<PageBuilderLinkedComponentContentChangedEvent>
{
    internal const int PageBatchSize = 500;

    public async Task Handle(PageBuilderLinkedComponentContentChangedEvent message)
    {
        var pageIds = await referenceIndexService.GetPageIdsAsync(message.LinkedComponentIds);
        if (pageIds.Count == 0)
        {
            return;
        }

        await referenceIndexService.TouchPagesAsync(pageIds);

        foreach (var pageIdBatch in BatchPageIds(pageIds))
        {
            foreach (var pageId in pageIdBatch)
            {
                var rawContent = await groupedPageService.LoadContent(pageId);
                var resolvedContent = await resolver.ResolveAsync(rawContent);
                await assetReferenceIndexService.RebuildPageIndexAsync(pageId, resolvedContent);
            }

            var pages = await pageService.GetAsync(pageIdBatch);
            var changedEntries = pages
                .Select(x => new GenericChangedEntry<PageBuilderPage>(x, EntryState.Modified))
                .ToArray();

            if (changedEntries.Length > 0)
            {
                await eventPublisher.Publish(new PageBuilderPageChangedEvent(changedEntries));
            }
        }
    }

    internal static IEnumerable<string[]> BatchPageIds(IEnumerable<string> pageIds)
    {
        return pageIds.Chunk(PageBatchSize);
    }
}
