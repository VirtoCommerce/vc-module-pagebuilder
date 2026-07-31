using VirtoCommerce.PageBuilderModule.Data.Repositories;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderWriteLockTests
{
    [Fact]
    public void OrderIds_DeduplicatesAndUsesStableGlobalOrder()
    {
        var first = PageBuilderWriteLock.OrderIds(["page-z", "Page-a", "page-b", "page-a", null, ""]);
        var second = PageBuilderWriteLock.OrderIds(["page-b", "page-a", "page-z"]);

        Assert.Equal(["Page-a", "page-b", "page-z"], first);
        Assert.Equal(["page-a", "page-b", "page-z"], second);
    }
}
