using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class PageBuilderSharedComponentChangingEvent(
    IEnumerable<GenericChangedEntry<PageBuilderSharedComponent>> changedEntries)
    : GenericChangedEntryEvent<PageBuilderSharedComponent>(changedEntries);
