using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Core.Events;

public class PageBuilderLinkedComponentContentChangedEvent(IEnumerable<string> linkedComponentIds) : IEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public int Version { get; set; }

    public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.UtcNow;

    public IList<string> LinkedComponentIds { get; } = linkedComponentIds
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
