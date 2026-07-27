using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers
{
    public class GroupedPageBuilderPageChangedEventHandler(
        IEventPublisher eventPublisher,
        IGroupedPageService groupedPageService,
        IPageBuilderAssetReferenceIndexService assetReferenceIndexService
    ) : PageBuilderEventHandlerBase(groupedPageService), IEventHandler<GroupedPageBuilderPageChangedEvent>
    {
        public async Task Handle(GroupedPageBuilderPageChangedEvent message)
        {
            await UpdateReferenceIndex(message);

            var eventTasks = message.ChangedEntries
                .SelectMany(x =>
                {
                    var groupedEntry = x.NewEntry ?? x.OldEntry;
                    return groupedEntry.Pages.Select(page => ToPagesDomainEvent(page, x.EntryState));
                })
                .ToArray();

            var events = await Task.WhenAll(eventTasks);

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
