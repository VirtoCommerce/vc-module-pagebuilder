namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderSharedComponentUsage
{
    public string SharedComponentId { get; set; }

    public int UsageCount { get; set; }

    public IList<PageBuilderSharedComponentUsagePage> Pages { get; set; } = [];
}
