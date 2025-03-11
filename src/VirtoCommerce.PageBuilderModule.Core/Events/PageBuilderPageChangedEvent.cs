using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class PageBuilderPageChangedEvent : GenericChangedEntryEvent<PageBuilderPage>
{
    public PageBuilderPageChangedEvent(IEnumerable<GenericChangedEntry<PageBuilderPage>> changedEntries)
        : base(changedEntries)
    {
    }
}
