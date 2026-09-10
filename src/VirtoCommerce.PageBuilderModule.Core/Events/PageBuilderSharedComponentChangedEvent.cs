using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class PageBuilderSharedComponentChangedEvent(
    IEnumerable<GenericChangedEntry<PageBuilderSharedComponent>> changedEntries)
    : GenericChangedEntryEvent<PageBuilderSharedComponent>(changedEntries);
