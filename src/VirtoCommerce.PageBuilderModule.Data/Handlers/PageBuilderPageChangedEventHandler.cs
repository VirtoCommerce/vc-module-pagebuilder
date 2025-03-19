using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Pages.Core.Events;
using VirtoCommerce.Pages.Core.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public class PageBuilderPageChangedEventHandler(IEventPublisher eventPublisher) : IEventHandler<PageBuilderPageChangedEvent>
{
    public async Task Handle(PageBuilderPageChangedEvent message)
    {
        var events = message.ChangedEntries.Select(x =>
        {
            var entry = x.NewEntry ?? x.OldEntry;
            var pageOperation = x.EntryState.ToPageOperation(entry);

            var pageDocument = entry.ToPageDocument();
            // todo: move to pages module
            pageDocument.Status = pageOperation.GetPageDocumentStatus();

            var result = AbstractTypeFactory<PagesDomainEvent>.TryCreateInstance();

            result.Operation = pageOperation;
            result.Page = pageDocument;

            return result;
        });

        await Task.WhenAll(
            events.ToList().Select(e => eventPublisher.Publish(e))
        );

    }
}
