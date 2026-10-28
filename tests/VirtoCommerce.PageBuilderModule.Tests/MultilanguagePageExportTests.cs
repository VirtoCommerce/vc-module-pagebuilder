using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.ExportImport;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class MultilanguagePageExportTests
{
    [Fact]
    public async Task DoExportAsync_WritesRootsBeforePagesAndLinksLanguageVersions()
    {
        var jsonSerializer = JsonSerializer.CreateDefault();
        var sharedComponents = new PageBuilderSharedComponentExportImport(
            new NoopSharedComponentReferenceIndexService(),
            null!,
            new FakeSharedComponentSearchService([]),
            null!,
            jsonSerializer);
        var exportImport = new PageBuilderExportImport(
            null!,
            new FakeGroupedPageSearchService([new GroupedPageBuilderPage { Id = "g-fr", CultureName = "fr-FR", MultilanguagePageId = "root-1" }]),
            null!,
            sharedComponents,
            null!,
            jsonSerializer,
            new MultilanguagePageExportImport(
                null,
                new FakeMultilanguagePageSearchService([new MultilanguagePage { Id = "root-1", Name = "FAQ" }]),
                jsonSerializer));

        await using var output = new MemoryStream();
        await exportImport.DoExportAsync(output, _ => { }, TestContext.Current.CancellationToken);

        var json = JObject.Parse(Encoding.UTF8.GetString(output.ToArray()));
        var properties = json.Properties().Select(x => x.Name).ToList();
        Assert.True(properties.IndexOf("PageBuilderMultilanguagePages") < properties.IndexOf("PageBuilderPages"));
        Assert.Equal("root-1", json["PageBuilderMultilanguagePages"]![0]!["Id"]!.ToString());
        Assert.Equal("root-1", json["PageBuilderPages"]![0]!["MultilanguagePageId"]!.ToString());
    }

    private class FakeSearchService<TCriteria, TResult, TModel>(IList<TModel> items) : ISearchService<TCriteria, TResult, TModel>
        where TCriteria : SearchCriteriaBase
        where TResult : GenericSearchResult<TModel>, new()
        where TModel : IEntity
    {
        public Task<TResult> SearchAsync(TCriteria criteria, bool clone = true)
        {
            return Task.FromResult(new TResult
            {
                TotalCount = items.Count,
                Results = criteria.Skip == 0 ? items : [],
            });
        }
    }

    private sealed class FakeSharedComponentSearchService(IList<PageBuilderSharedComponent> items)
        : FakeSearchService<PageBuilderSharedComponentSearchCriteria, PageBuilderSharedComponentSearchResult, PageBuilderSharedComponent>(items),
          IPageBuilderSharedComponentSearchService;

    private sealed class FakeGroupedPageSearchService(IList<GroupedPageBuilderPage> items)
        : FakeSearchService<PageBuilderPageSearchCriteria, GroupedPageBuilderPageSearchResult, GroupedPageBuilderPage>(items),
          IGroupedPageSearchService;

    private sealed class FakeMultilanguagePageSearchService(IList<MultilanguagePage> items)
        : FakeSearchService<MultilanguagePageSearchCriteria, MultilanguagePageSearchResult, MultilanguagePage>(items),
          IMultilanguagePageSearchService;
}
