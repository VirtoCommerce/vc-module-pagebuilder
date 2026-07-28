namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderLinkedComponentUsage
{
    public string LinkedComponentId { get; set; }

    public int UsageCount { get; set; }

    public IList<PageBuilderLinkedComponentUsagePage> Pages { get; set; } = [];
}
