using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers
{
    public class GroupedPageBuilderPageChangedEventHandler(
        IEventPublisher eventPublisher,
        IGroupedPageService groupedPageService,
        IPageBuilderSharedComponentResolver sharedComponentResolver,
        IPageBuilderAssetReferenceIndexService assetReferenceIndexService,
        ILogger<GroupedPageBuilderPageChangedEventHandler> logger
    ) : PageBuilderEventHandlerBase(groupedPageService, sharedComponentResolver, logger), IEventHandler<GroupedPageBuilderPageChangedEvent>
    {
        public async Task Handle(GroupedPageBuilderPageChangedEvent message)
        {
            await UpdateReferenceIndex(message);

            var pageChanges = message.ChangedEntries
                .SelectMany(x =>
                {
                    var groupedEntry = x.NewEntry ?? x.OldEntry;
                    return groupedEntry.Pages.Select(page => (Page: page, x.EntryState));
                })
                .ToArray();

            var events = await SelectBoundedAsync(
                pageChanges,
                x => ToPagesDomainEvent(x.Page, x.EntryState));

            await PublishPagesDomainEvents(events, eventPublisher);
        }

        private async Task UpdateReferenceIndex(GroupedPageBuilderPageChangedEvent message)
        {
            var deletedGroupIds = message.ChangedEntries
                .Where(x => x.EntryState == EntryState.Deleted)
                .Select(x => x.OldEntry?.Id ?? x.NewEntry?.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            await assetReferenceIndexService.DeleteGroupIndexAsync(deletedGroupIds);
        }
    }
}
