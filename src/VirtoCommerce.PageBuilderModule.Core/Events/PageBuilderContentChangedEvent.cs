using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

[method: JsonConstructor]
public class PageBuilderContentChangedEvent(
    string contentType,
    IEnumerable<GenericChangedEntry<FileEntity>> changedEntries)
    : GenericChangedEntryEvent<FileEntity>(changedEntries)
{
    public string ContentType { get; } = contentType;
}
