using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public class PageBuilderPageChangedEventHandler(
    IEventPublisher eventPublisher,
    IGroupedPageService groupedPageService
) : PageBuilderEventHandlerBase(groupedPageService), IEventHandler<PageBuilderPageChangedEvent>
{
    public async Task Handle(PageBuilderPageChangedEvent message)
    {
        var eventTasks = message.ChangedEntries.Select(x =>
        {
            var page = x.NewEntry ?? x.OldEntry;
            return ToPagesDomainEvent(page, x.EntryState);
        });

        var events = await Task.WhenAll(eventTasks);

        await PublishPagesDomainEvents(events, eventPublisher);
    }
}
