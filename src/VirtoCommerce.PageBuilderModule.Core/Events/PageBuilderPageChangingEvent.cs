using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class PageBuilderPageChangingEvent : GenericChangedEntryEvent<PageBuilderPage>
{
    public PageBuilderPageChangingEvent(IEnumerable<GenericChangedEntry<PageBuilderPage>> changedEntries)
        : base(changedEntries)
    {
    }
}
