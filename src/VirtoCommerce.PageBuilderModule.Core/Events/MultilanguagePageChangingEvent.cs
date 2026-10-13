using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class MultilanguagePageChangingEvent(IEnumerable<GenericChangedEntry<MultilanguagePage>> changedEntries)
    : GenericChangedEntryEvent<MultilanguagePage>(changedEntries);
