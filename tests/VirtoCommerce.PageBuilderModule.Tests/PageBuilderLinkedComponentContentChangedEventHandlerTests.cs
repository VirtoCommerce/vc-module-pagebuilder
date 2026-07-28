using System.Linq;
using VirtoCommerce.PageBuilderModule.Data.Handlers;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentContentChangedEventHandlerTests
{
    [Fact]
    public void BatchPageIds_KeepsEveryDatabaseAndEventBatchBelowSqlServerParameterLimit()
    {
        var pageIds = Enumerable.Range(0, 1201).Select(x => $"page-{x}").ToArray();

        var batches = PageBuilderLinkedComponentContentChangedEventHandler
            .BatchPageIds(pageIds)
            .ToArray();

        Assert.Equal([500, 500, 201], batches.Select(x => x.Length));
        Assert.Equal(pageIds, batches.SelectMany(x => x));
    }
}
