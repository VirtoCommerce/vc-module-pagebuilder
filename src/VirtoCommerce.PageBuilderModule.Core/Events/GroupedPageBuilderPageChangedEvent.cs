using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class GroupedPageBuilderPageChangedEvent(IEnumerable<GenericChangedEntry<GroupedPageBuilderPage>> changedEntries)
    : GenericChangedEntryEvent<GroupedPageBuilderPage>(changedEntries);
