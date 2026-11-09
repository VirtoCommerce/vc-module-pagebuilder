using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class SchemaCatalogServiceTests
{
    private const string Catalog = """{"hash":"abcdefgh","sections":{},"blocks":{},"shared":{}}""";
    private const string CatalogFile = "default/catalogs/abcdefgh.json";
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

    private SchemaCatalogService Service() => new(new ThemeStorageFactory(_storage.StoreService, _storage, _storage, _storage));
}
