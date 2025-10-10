using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers
{
    public class GroupedPageBuilderPageChangedEventHandler(
        IEventPublisher eventPublisher,
        IGroupedPageService groupedPageService
    ) : PageBuilderEventHandlerBase(groupedPageService), IEventHandler<GroupedPageBuilderPageChangedEvent>
    {
        public async Task Handle(GroupedPageBuilderPageChangedEvent message)
        {
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
    }
}
