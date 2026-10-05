namespace VirtoCommerce.PageBuilderModule.Core.Models;

public sealed record PageBuilderConditionalContentWriteResult(string PageId, string ETag)
{
    public bool ContentChanged { get; init; } = true;
    public bool GroupEventsPublished { get; init; }
}
