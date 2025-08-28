using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Pages.Core.Events;
using VirtoCommerce.Pages.Core.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public abstract class PageBuilderEventHandlerBase(IPageBuilderPageService pageBuilderPageService)
{
    protected async Task<PagesDomainEvent> ToPagesDomainEvent(PageBuilderPage entry, EntryState state)
    {
        var pageOperation = state.ToPageOperation(entry);

        var content = await pageBuilderPageService.GetPageContentAsync(entry.Id);

        var pageDocument = entry.ToPageDocument(content);
        // todo: move to pages module
        pageDocument.Status = pageOperation.GetPageDocumentStatus();

        var result = AbstractTypeFactory<PagesDomainEvent>.TryCreateInstance();

        result.Operation = pageOperation;
        result.Page = pageDocument;

        return result;
    }

    protected static async Task PublishPagesDomainEvents(IEnumerable<PagesDomainEvent> events, IEventPublisher eventPublisher)
    {
        await Task.WhenAll(events.Select(e => eventPublisher.Publish(e)));
    }
}

public class PageBuilderPageChangedEventHandler(IEventPublisher eventPublisher, IPageBuilderPageService pageBuilderPageService) : PageBuilderEventHandlerBase(pageBuilderPageService), IEventHandler<PageBuilderPageChangedEvent>
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

public class GroupedPageBuilderPageChangedEventHandler(IEventPublisher eventPublisher, IPageBuilderPageService pageBuilderPageService) : PageBuilderEventHandlerBase(pageBuilderPageService), IEventHandler<GroupedPageBuilderPageChangedEvent>
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
