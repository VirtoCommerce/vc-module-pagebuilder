using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Pages.Core.Events;
using VirtoCommerce.Pages.Core.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers
{
    public abstract class PageBuilderEventHandlerBase(
        IGroupedPageService groupedPageService
    )
    {
        protected async Task<PagesDomainEvent> ToPagesDomainEvent(PageBuilderPage entry, EntryState state)
        {
            var pageOperation = state.ToPageOperation(entry);

            var group = await groupedPageService.GetByIdAsync(entry.GroupId);
            var content = await groupedPageService.LoadContent(entry.Id);

            var pageDocument = entry.ToPageDocument(group, content);
            // todo: move to pages module
            pageDocument.Status = pageOperation.GetPageDocumentStatus();

            var result = AbstractTypeFactory<PagesDomainEvent>.TryCreateInstance();

            result.Id = pageDocument.Id;
            result.Operation = pageOperation;
            result.Page = pageDocument;

            return result;
        }

        protected static async Task PublishPagesDomainEvents(IEnumerable<PagesDomainEvent> events, IEventPublisher eventPublisher)
        {
            await Task.WhenAll(events.Select(e => eventPublisher.Publish(e)));
        }
    }
}
