namespace VirtoCommerce.PageBuilderModule.Core.Models;

public sealed record PageBuilderConditionalContentWriteResult(string PageId, string ETag)
{
    public bool ContentWritten { get; init; } = true;
    public bool GroupedEventsPublished { get; init; }
}
