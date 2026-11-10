using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class SchemaCatalogServiceTests
{
    private const string Catalog = """{"hash":"abcdefgh","sections":{},"blocks":{},"shared":{}}""";
    private const string CatalogFile = "themes/s1/default/catalogs/abcdefgh.json";
    private const string CutOff = """{"hash":"abcdefgh","sect""";

    private readonly FakeBlobStorage _storage = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_FileCutOffMidWrite_AnswersAsIfThereWereNone()
    {
        _storage.Files[CatalogFile] = CutOff;

        Assert.Null(await Service().GetAsync("s1", "abcdefgh", Ct));
    }

    [Fact]
    public async Task SaveAsync_FileCutOffMidWrite_WritesItAgain()
    {
        _storage.Files[CatalogFile] = CutOff;

        await Service().SaveAsync("s1", Catalog, Ct);

        Assert.Equal(Catalog, _storage.Files[CatalogFile]);
    }

    [Fact]
    public async Task SaveAsync_FileAlreadyReadable_DoesNotWriteItAgain()
    {
        _storage.Files[CatalogFile] = Catalog;

        await Service().SaveAsync("s1", Catalog, Ct);

        Assert.DoesNotContain(CatalogFile, _storage.Written);
    }

    [Fact]
    public async Task SaveAsync_SameHashTwice_KeepsFirstContentAndMakesItCurrent()
    {
        var catalogs = Service();
        await catalogs.SaveAsync("s1", """{ "hash": "abc12345", "sections": { "a": {} } }""", Ct);
        await catalogs.SaveAsync("s1", """{ "hash": "abc12345", "sections": { "b": {} } }""", Ct);

        Assert.NotNull(JObject.Parse(await catalogs.GetAsync("s1", "abc12345", Ct))["sections"]!["a"]);
        Assert.Equal(await catalogs.GetAsync("s1", "abc12345", Ct), await catalogs.GetAsync("s1", null, Ct));
    }

    [Fact]
    public async Task SaveAsync_HashThatIsAPath_ThrowsAndKeepsNothing()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().SaveAsync("s1", """{ "hash": "../x" }""", Ct));

        Assert.Empty(_storage.Written);
    }

    [Theory]
    [InlineData("""{ "hash": {} }""")]
    [InlineData("""{ "hash": ["abc12345"] }""")]
    [InlineData("""{ "hash": "abc12345", "sections": [] }""")]
    [InlineData("""{ "hash": "abc12345", "blocks": "banner" }""")]
    [InlineData("""{ "hash": "abc12345", "shared": { "_sections": [] } }""")]
    public async Task SaveAsync_UnreadableCatalog_ThrowsAndKeepsNothing(string catalog)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().SaveAsync("s1", catalog, Ct));

        Assert.Null(await Service().GetAsync("s1", null, Ct));
    }

    private SchemaCatalogService Service() => new(new ThemeStorageFactory(_storage.StoreService, _storage, _storage, _storage));
}
