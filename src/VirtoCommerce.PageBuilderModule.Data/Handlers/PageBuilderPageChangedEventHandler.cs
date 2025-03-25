using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Pages.Core.Events;
using VirtoCommerce.Pages.Core.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public abstract class PageBuilderEventHandlerBase
{
    protected static PagesDomainEvent ToPagesDomainEvent(PageBuilderPage entry, EntryState state)
    {
        var pageOperation = state.ToPageOperation(entry);

        var pageDocument = entry.ToPageDocument();
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

public class PageBuilderPageChangedEventHandler(IEventPublisher eventPublisher) : PageBuilderEventHandlerBase, IEventHandler<PageBuilderPageChangedEvent>
{
    public async Task Handle(PageBuilderPageChangedEvent message)
    {
        var events = message.ChangedEntries.Select(x =>
        {
            var page = x.NewEntry ?? x.OldEntry;
            return ToPagesDomainEvent(page, x.EntryState);
        });

        await PublishPagesDomainEvents(events, eventPublisher);
    }
}

public class GroupedPageBuilderPageChangedEventHandler(IEventPublisher eventPublisher) : PageBuilderEventHandlerBase, IEventHandler<GroupedPageBuilderPageChangedEvent>
{
    public async Task Handle(GroupedPageBuilderPageChangedEvent message)
    {
        var events = message.ChangedEntries.SelectMany(x =>
        {
            var groupedEntry = x.NewEntry ?? x.OldEntry;

            var results = new List<PagesDomainEvent>();
            foreach (var page in groupedEntry.Pages)
            {
                var result = ToPagesDomainEvent(page, x.EntryState);
                results.Add(result);
            }

            return results;
        });

        await PublishPagesDomainEvents(events, eventPublisher);
    }
}
