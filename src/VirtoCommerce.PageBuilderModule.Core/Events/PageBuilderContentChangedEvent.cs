using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

[method: JsonConstructor]
public class PageBuilderContentChangedEvent(
    string contentType,
    IEnumerable<GenericChangedEntry<FileEntity>> changedEntries)
    : GenericChangedEntryEvent<FileEntity>(changedEntries)
{
    public string ContentType { get; } = contentType;

    /// <summary>A changed file, as the registered <see cref="FileEntity"/> type.</summary>
    public static GenericChangedEntry<FileEntity> ChangedFile(string path, string type, EntryState state)
    {
        var file = AbstractTypeFactory<FileEntity>.TryCreateInstance();
        file.Path = path;
        file.Type = type;
        return new GenericChangedEntry<FileEntity>(file, state);
    }
}
