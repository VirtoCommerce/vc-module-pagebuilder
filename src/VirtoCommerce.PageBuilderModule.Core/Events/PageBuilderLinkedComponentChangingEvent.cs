using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class PageBuilderLinkedComponentChangingEvent(
    IEnumerable<GenericChangedEntry<PageBuilderLinkedComponent>> changedEntries)
    : GenericChangedEntryEvent<PageBuilderLinkedComponent>(changedEntries);
