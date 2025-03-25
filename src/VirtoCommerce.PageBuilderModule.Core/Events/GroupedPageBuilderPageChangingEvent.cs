using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class GroupedPageBuilderPageChangingEvent : GenericChangedEntryEvent<GroupedPageBuilderPage>
{
    public GroupedPageBuilderPageChangingEvent(IEnumerable<GenericChangedEntry<GroupedPageBuilderPage>> changedEntries)
        : base(changedEntries)
    {
    }
}
