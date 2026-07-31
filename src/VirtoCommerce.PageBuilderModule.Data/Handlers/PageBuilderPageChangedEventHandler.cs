using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public class PageBuilderPageChangedEventHandler(
    IEventPublisher eventPublisher,
    IGroupedPageService groupedPageService,
    IPageBuilderLinkedComponentResolver linkedComponentResolver,
    IPageBuilderAssetReferenceIndexService assetReferenceIndexService
) : PageBuilderEventHandlerBase(groupedPageService, linkedComponentResolver), IEventHandler<PageBuilderPageChangedEvent>
{
    public async Task Handle(PageBuilderPageChangedEvent message)
    {
        await UpdateReferenceIndex(message);

        var changedEntries = message.ChangedEntries.ToArray();
        var events = await SelectBoundedAsync(
            changedEntries,
            x =>
            {
                var page = x.NewEntry ?? x.OldEntry;
                return ToPagesDomainEvent(page, x.EntryState);
            });

        await PublishPagesDomainEvents(events, eventPublisher);
    }

    private async Task UpdateReferenceIndex(PageBuilderPageChangedEvent message)
    {
        var deletedPageIds = message.ChangedEntries
            .Where(x => x.EntryState == EntryState.Deleted)
            .Select(x => x.OldEntry?.Id ?? x.NewEntry?.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        await assetReferenceIndexService.DeletePageIndexAsync(deletedPageIds);
    }
}
