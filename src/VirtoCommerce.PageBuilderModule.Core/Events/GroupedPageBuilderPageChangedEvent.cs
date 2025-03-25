using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class GroupedPageBuilderPageChangedEvent : GenericChangedEntryEvent<GroupedPageBuilderPage>
{
    public GroupedPageBuilderPageChangedEvent(IEnumerable<GenericChangedEntry<GroupedPageBuilderPage>> changedEntries)
        : base(changedEntries)
    {
    }
}
