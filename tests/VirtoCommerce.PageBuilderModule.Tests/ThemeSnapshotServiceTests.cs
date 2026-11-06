using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public sealed class ThemeSnapshotServiceTests : IDisposable
{
    private const string Product = "default/templates/product.json";
    private const string Pointer = "default/snapshots/current.json";

    private readonly FakeBlobStorage _storage = new();
    private readonly FakeSettingsManager _settings = new();
    private readonly TestPlatformMemoryCache _cache = new();
    private readonly InProcessDistributedLock _lock = new();
    private readonly TestThemeSnapshotService _snapshots;
    private readonly ThemePublisher _publisher;
    private readonly SchemaCatalogService _catalogs;
    private readonly Store _store = new() { Id = "s1", DefaultLanguage = "en-US", Languages = ["en-US", "de-DE"], DynamicProperties = [] };
    private readonly FakeStoreService _stores;
    private readonly ThemeStorageFactory _storageFactory;

    public ThemeSnapshotServiceTests()
    {
        _stores = new FakeStoreService(_store);
        _storageFactory = new ThemeStorageFactory(_stores, _storage, _storage, _storage);
        _catalogs = new SchemaCatalogService(_storageFactory);
        _publisher = Publisher(new NoContentHistory());
        _snapshots = Snapshots(_cache);
        _settings.Values[ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder.Name] = true;
    }

    private static System.Threading.CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetPublishedAsync_NewPageBuilderOff_ReturnsNull()
    {
        _settings.Values[ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder.Name] = false;
        _storage.Files[Product] = Template("product-info");

        Assert.Null(await _snapshots.GetPublishedAsync("s1", "en-US", Ct));
        Assert.Empty(_storage.Written);
    }

    [Fact]
    public async Task GetPublishedAsync_FirstRead_PublishesLiveThemeAndKeepsDrafts()
    {
        _storage.Files[Product] = Template("product-info");
        _storage.Files[Product + "-draft"] = Template("product-info", "draft-only");

        var snapshot = await _snapshots.GetPublishedAsync("s1", null, Ct);

        Assert.Equal((1, "en-US"), (snapshot.Publication, snapshot.CultureName));
        Assert.Equal(["product-info"], Sections(snapshot, "product"));
        Assert.True(_storage.Files.ContainsKey(Product + "-draft"));
    }

    [Fact]
    public async Task PublishAsync_Drafts_WritesLanguagesRecordPointerThenPutsDraftsLive()
    {
        _storage.Files[Product] = Template("product-info");
        _storage.Files[Product + "-draft"] = Template("product-info", "related-products");
        await _snapshots.GetPublishedAsync("s1", null, Ct);
        var catalog = await SaveCatalogAsync("product-info", "related-products");
        _storage.Written.Clear();

        var publication = await _publisher.PublishAsync("s1", catalog, null, Ct);

        Assert.Equal(2, publication);
        Assert.Equal(["default/snapshots/2/de-DE.json", "default/snapshots/2/en-US.json"], _storage.Written.Take(2).Order());
        // the record marks a complete publication (a pointer cut off falls back on it)
        Assert.Equal([Record(2), Pointer, Product], _storage.Written.Skip(2));
        Assert.Equal("en-US", (await (await _storageFactory.OpenAsync("s1")).GetPublicationRecordAsync(2)).DefaultLanguage);
        Assert.False(_storage.Files.ContainsKey(Product + "-draft"));
        Assert.Equal(["product-info", "related-products"], Sections(await _snapshots.GetPublishedAsync("s1", "de", Ct), "product"));
    }

    [Fact]
    public async Task PublishAsync_Published_WritesThePointerAndTheRecordInTheirStorageFormat()
    {
        _storage.Files[Product + "-draft"] = Template("product-info");
        await _catalogs.SaveAsync("s1", """{ "hash": "abc12345", "sections": { "product-info": {} } }""", Ct);

        await _publisher.PublishAsync("s1", "abc12345", null, Ct);

        Assert.Equal("""{"publication":1}""", _storage.Files[Pointer]);
        Assert.Equal("""{"defaultLanguage":"en-US"}""", _storage.Files[Record(1)]);
        Assert.Equal("""{"hash":"abc12345"}""", _storage.Files["default/catalogs/current.json"]);
        Assert.Equal("""{"hash":"abc12345","sections":{"product-info":{}}}""", _storage.Files["default/catalogs/abc12345.json"]);
    }

    [Fact]
    public async Task PublishAsync_Published_RecordsHistoryAfterStoreLockIsReleased()
    {
        // GitHub may answer slowly: the store's theme saves must not wait for it
        _storage.Files[Product] = Template("product-info");
        await _snapshots.GetPublishedAsync("s1", null, Ct);
        _storage.Files[Product + "-draft"] = Template("product-info", "related-products");
        var catalog = await SaveCatalogAsync("product-info", "related-products");
        var history = new LockProbingHistory(_lock);
        var publisher = Publisher(history);

        await publisher.PublishAsync("s1", catalog, null, Ct);

        var (record, lockFree) = Assert.Single(history.Recorded);
        Assert.True(lockFree);
        Assert.Equal([$"theme:{Product}"], record.Entries.Select(x => x.Item.ToString()));
    }

    [Fact]
    public async Task PublishAsync_SeveralDrafts_RecordsThemAsOneVersion()
    {
        _storage.Files[Product] = Template("product-info");
        await _snapshots.GetPublishedAsync("s1", null, Ct);
        var header = Template("product-info");
        _storage.Files[Product + "-draft"] = Template("product-info", "related-products");
        _storage.Files["default/sections/header-group.json-draft"] = header;
        var catalog = await SaveCatalogAsync("product-info", "related-products");
        var history = new RecordingContentHistory();

        await Publisher(history).PublishAsync("s1", catalog, null, Ct);

        var (storeId, record) = Assert.Single(history.Records);
        Assert.Equal("s1", storeId);
        Assert.Equal(ModuleConstants.GitHistory.PublishedSummary, record.Description);
        Assert.Equal(["theme:default/sections/header-group.json", $"theme:{Product}"], record.Entries.Select(x => x.Item.ToString()).Order());
        Assert.Equal(header, record.Entries.Single(x => x.Item.Key == "default/sections/header-group.json").Content);
    }

    [Fact]
    public async Task GetStatusAsync_BeforeFirstPublication_ListsDraftsWithoutPublication()
    {
        _storage.Files[Product] = Template("product-info");
        _storage.Files["default/templates/product.b2c.json-draft"] = Template("product-info");
        _storage.Files["default/sections/header-group.json-draft"] = """{ "content": [] }""";
        _storage.Files["default/config/settings_data.json"] = "{}";

        var status = await _publisher.GetStatusAsync("s1", Ct);

        Assert.Null(status.Publication);
        Assert.Equal(["sections/header-group.json", "templates/product.b2c.json"], status.Drafts.Order());
    }

    [Fact]
    public async Task GetStatusAsync_AfterPublication_ReturnsPublicationAndNoDrafts()
    {
        _storage.Files[Product + "-draft"] = Template("product-info");
        _storage.Files["default/config/template_rules.json-draft"] = """{ "version": 1 }""";
        var catalog = await SaveCatalogAsync("product-info");
        Assert.Equal(["config/template_rules.json", "templates/product.json"], (await _publisher.GetStatusAsync("s1", Ct)).Drafts.Order());

        var publication = await _publisher.PublishAsync("s1", catalog, null, Ct);

        var status = await _publisher.GetStatusAsync("s1", Ct);
        Assert.Equal(publication, status.Publication);
        Assert.Empty(status.Drafts);
    }

    [Fact]
    public async Task ValidateFilesAsync_FilesOfTypesCatalogHas_Passes()
    {
        var catalog = await SaveCatalogAsync("product-info", "site-header");

        await _publisher.ValidateFilesAsync("s1", catalog, new Dictionary<string, JToken>
        {
            ["templates/product.json"] = JToken.Parse(Template("product-info")),
            ["sections/header-group.json"] = JToken.Parse(Template("site-header")),
            ["config/settings_data.json"] = new JObject { ["current"] = new JObject() },
        }, Ct);
    }

    [Theory]
    [InlineData("templates/product.json")]
    [InlineData("sections/header-group.json")]
    [InlineData("sections/footer-group.json")]
    public async Task ValidateFilesAsync_TypeCatalogLacks_ThrowsWithFileAndType(string file)
    {
        var catalog = await SaveCatalogAsync("product-info");

        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() =>
            _publisher.ValidateFilesAsync("s1", catalog, new Dictionary<string, JToken> { [file] = JToken.Parse(Template("product-info", "carousel")) }, Ct));

        Assert.Contains("'carousel'", failure.Errors.Single());
    }

    [Fact]
    public async Task ValidateFilesAsync_UnknownCatalogHash_Throws()
    {
        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() =>
            _publisher.ValidateFilesAsync("s1", "missing123", new Dictionary<string, JToken> { ["templates/product.json"] = JToken.Parse(Template("product-info")) }, Ct));

        Assert.Contains("missing123", failure.Errors.Single());
    }

    [Fact]
    public async Task PublishAsync_CatalogRefusesDraft_WritesNothing()
    {
        await _catalogs.SaveAsync("s1", """{ "hash": "abc12345", "sections": { "product-info": {} }, "blocks": {}, "shared": {} }""", Ct);
        _storage.Files[Product + "-draft"] = Template("product-info", "carousel");
        _storage.Written.Clear();

        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync("s1", "abc12345", null, Ct));

        Assert.Contains("'carousel'", failure.Errors.Single());
        Assert.Empty(_storage.Written);
        Assert.True(_storage.Files.ContainsKey(Product + "-draft"));
    }

    [Fact]
    public async Task PublishAsync_NoCatalogHash_ThrowsAndWritesNothing()
    {
        _storage.Files[Product + "-draft"] = Template("product-info");

        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync("s1", null, null, Ct));

        Assert.Contains("catalog", failure.Errors.Single());
        Assert.Empty(_storage.Written);
    }

    [Fact]
    public async Task GetPublishedAsync_FirstPublication_FindsMenusThroughCurrentCatalog()
    {
        await _catalogs.SaveAsync("s1", """
            { "hash": "abc12345", "sections": { "site-header": { "settings": [ { "id": "menu", "type": "menu" } ] } } }
            """, Ct);
        _storage.Files["default/sections/header-group.json"] = """
            { "content": [ { "id": "h", "type": "site-header", "menu": "main-menu" } ] }
            """;

        var snapshot = await _snapshots.GetPublishedAsync("s1", null, Ct);

        Assert.Equal(["main-menu"], snapshot.Menus);
        // validated against no catalog: the live files may predate the storefront's schemas
        Assert.Equal(JTokenType.Null, JObject.Parse(snapshot.Content)["catalogHash"]!.Type);
    }

    [Fact]
    public async Task PublishAsync_UnknownCatalogHash_Throws()
    {
        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync("s1", "missing123", null, Ct));

        Assert.Contains("missing123", failure.Errors.Single());
    }

    [Fact]
    public async Task GetPublishedAsync_AfterPublication_ReturnsNewPublication()
    {
        _storage.Files[Product] = Template("product-info");
        Assert.Equal(1, (await _snapshots.GetPublishedAsync("s1", "en-US", Ct)).Publication);

        _storage.Files[Product + "-draft"] = Template("product-info", "related-products");
        await _publisher.PublishAsync("s1", await SaveCatalogAsync("product-info", "related-products"), null, Ct);

        Assert.Equal(2, (await _snapshots.GetPublishedAsync("s1", "en-US", Ct)).Publication);
    }

    [Fact]
    public async Task PublishAsync_ThemeSaveRunning_WaitsForStoreLock()
    {
        _storage.Files[Product + "-draft"] = Template("product-info");
        var catalog = await SaveCatalogAsync("product-info");
        var save = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);

        var publication = _publisher.PublishAsync("s1", catalog, null, Ct);

        Assert.Equal(ThemeStorage.GetLockResource("s1"), await _lock.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.DoesNotContain(_storage.Written, x => x.StartsWith("default/snapshots/", StringComparison.Ordinal));

        save.Dispose();
        Assert.Equal(1, await publication);
    }

    [Fact]
    public async Task GetPublishedAsync_CachingOff_ReadsSnapshotAgain()
    {
        using var uncached = new TestPlatformMemoryCache(() => new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromTicks(1) });
        var snapshots = Snapshots(uncached);
        _storage.Files[Product] = Template("product-info");
        await snapshots.GetPublishedAsync("s1", "en-US", Ct);
        // read once more: the first read published, which resets what it would have kept
        var read = await snapshots.GetPublishedAsync("s1", "en-US", Ct);

        _storage.Files["default/snapshots/1/en-US.json"] = read.Content.Replace("product-info", "related-products");

        Assert.Contains("related-products", (await snapshots.GetPublishedAsync("s1", "en-US", Ct)).Content);
    }

    [Fact]
    public async Task GetPublishedAsync_UnpublishableLiveFiles_CachesFailureUntilReset()
    {
        const string header = "default/sections/header-group.json";
        _storage.Files[header] = """{ "content": [ { "type": "banner" } ] }""";
        await Assert.ThrowsAsync<ThemeValidationException>(() => _snapshots.GetPublishedAsync("s1", "en-US", Ct));

        // the next read gets the same answer without waiting for the store's lock
        _lock.Hold();
        await Assert.ThrowsAsync<ThemeValidationException>(() => _snapshots.GetPublishedAsync("s1", "de-DE", Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct));
        _lock.Release();

        _storage.Files[header] = """{ "content": [ { "id": "h", "type": "banner" } ] }""";
        ThemeSnapshotCacheRegion.ExpireTokenForKey("s1");
        Assert.Equal(1, (await _snapshots.GetPublishedAsync("s1", "en-US", Ct)).Publication);
    }

    [Fact]
    public async Task GetPublishedAsync_TruncatedPointer_ReturnsHighestCompletePublication()
    {
        _storage.Files[Product] = Template("product-info");
        await _snapshots.GetPublishedAsync("s1", null, Ct);
        _storage.Files[Product + "-draft"] = Template("product-info", "related-products");
        await _publisher.PublishAsync("s1", await SaveCatalogAsync("product-info", "related-products"), null, Ct);
        _storage.Files[Pointer] = "{ \"publicat";
        using var emptyCache = new TestPlatformMemoryCache();

        var snapshot = await Snapshots(emptyCache).GetPublishedAsync("s1", null, Ct);

        Assert.Equal(2, snapshot.Publication);
    }

    [Fact]
    public async Task GetPublishedAsync_TruncatedPointerAndRecord_SkipsIncompletePublication()
    {
        _storage.Files[Product] = Template("product-info");
        await _snapshots.GetPublishedAsync("s1", null, Ct);
        // publication 2 stopped while writing its record, the file written last
        _storage.Files["default/snapshots/2/de-DE.json"] = "{}";
        _storage.Files["default/snapshots/2/en-US.json"] = "{}";
        _storage.Files[Record(2)] = "{\"defaultLangu";
        _storage.Files[Pointer] = "{ \"publicat";
        using var emptyCache = new TestPlatformMemoryCache();

        var snapshot = await Snapshots(emptyCache).GetPublishedAsync("s1", null, Ct);

        Assert.Equal(1, snapshot.Publication);
    }

    [Fact]
    public async Task GetPublishedAsync_StoreDefaultWithoutFile_ReturnsPublicationDefault()
    {
        _storage.Files[Product] = Template("product-info");
        await _snapshots.GetPublishedAsync("s1", null, Ct);
        _store.Languages = ["en-US", "de-DE", "fr-FR"];
        _store.DefaultLanguage = "fr-FR";

        var snapshot = await _snapshots.GetPublishedAsync("s1", null, Ct);

        Assert.Equal((1, "en-US"), (snapshot.Publication, snapshot.CultureName));
    }

    [Fact]
    public async Task GetPublishedAsync_TruncatedPointerAfterDefaultLanguageChanged_ReturnsCompletePublication()
    {
        _storage.Files[Product] = Template("product-info");
        await _snapshots.GetPublishedAsync("s1", null, Ct);
        _store.Languages = ["en-US", "de-DE", "fr-FR"];
        _store.DefaultLanguage = "fr-FR";
        _storage.Files[Pointer] = "{ \"publicat";

        var snapshot = await _snapshots.GetPublishedAsync("s1", null, Ct);

        Assert.Equal((1, "en-US"), (snapshot.Publication, snapshot.CultureName));
    }

    [Fact]
    public async Task GetPublishedAsync_TruncatedPointerWithoutCompletePublication_PublishesLiveThemeAgain()
    {
        _storage.Files[Product] = Template("product-info");
        // publication 1 stopped before its record
        _storage.Files["default/snapshots/1/de-DE.json"] = "{}";
        _storage.Files[Pointer] = "{ \"publicat";

        var snapshot = await _snapshots.GetPublishedAsync("s1", null, Ct);

        Assert.Equal((2, "en-US"), (snapshot.Publication, snapshot.CultureName));
        Assert.Equal(["product-info"], Sections(snapshot, "product"));
    }

    [Theory]
    [InlineData("de-DE", "de-DE")]
    [InlineData("DE-de", "de-DE")]
    [InlineData("de", "de-DE")]
    [InlineData("en-GB", "en-US")]
    [InlineData("fr-FR", "en-US")]
    [InlineData(null, "en-US")]
    [InlineData("", "en-US")]
    public void ResolveLanguage_RequestedLanguage_ReturnsClosestStoreLanguage(string requested, string served)
    {
        Assert.Equal(served, _snapshots.ResolveLanguagePublic(["en-US", "de-DE", "de-AT"], "en-US", requested));
    }

    [Fact]
    public void ResolveLanguage_SameLanguageTwice_PrefersDefault()
    {
        Assert.Equal("de-AT", _snapshots.ResolveLanguagePublic(["de-DE", "de-AT"], "de-AT", "de"));
    }

    [Fact]
    public async Task SaveAsync_SameHashTwice_KeepsFirstContentAndMakesItCurrent()
    {
        await _catalogs.SaveAsync("s1", """{ "hash": "abc12345", "sections": { "a": {} } }""", Ct);
        await _catalogs.SaveAsync("s1", """{ "hash": "abc12345", "sections": { "b": {} } }""", Ct);

        Assert.NotNull(JObject.Parse(await _catalogs.GetAsync("s1", "abc12345", Ct))["sections"]!["a"]);
        Assert.Equal(await _catalogs.GetAsync("s1", "abc12345", Ct), await _catalogs.GetAsync("s1", null, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => _catalogs.SaveAsync("s1", """{ "hash": "../x" }""", Ct));
    }

    [Theory]
    [InlineData("""{ "hash": {} }""")]
    [InlineData("""{ "hash": ["abc12345"] }""")]
    [InlineData("""{ "hash": "abc12345", "sections": [] }""")]
    [InlineData("""{ "hash": "abc12345", "blocks": "banner" }""")]
    [InlineData("""{ "hash": "abc12345", "shared": { "_sections": [] } }""")]
    public async Task SaveAsync_UnreadableCatalog_ThrowsAndKeepsNothing(string catalog)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _catalogs.SaveAsync("s1", catalog, Ct));

        Assert.Null(await _catalogs.GetAsync("s1", null, Ct));
    }

    private Task<string> SaveCatalogAsync(params string[] sections) =>
        _catalogs.SaveAsync("s1", new JObject
        {
            ["hash"] = "catalog-" + string.Join('-', sections),
            ["sections"] = new JObject(sections.Select(x => new JProperty(x, new JObject()))),
        }.ToString(), Ct);

    private ThemePublisher Publisher(IContentHistory history) =>
        new(_storageFactory, new ThemeSnapshotBuilder(), _stores, _catalogs, _lock, _storage, history,
            Options.Create(new ThemeSnapshotOptions()), NullLogger<ThemePublisher>.Instance);

    private TestThemeSnapshotService Snapshots(TestPlatformMemoryCache cache) =>
        new(_stores, _settings, _storageFactory, _publisher, cache, Options.Create(new ThemeSnapshotOptions()));

    private static string Record(int publication) => $"default/snapshots/{publication}/publication.json";

    private static string Template(params string[] sections) =>
        new JObject
        {
            ["settings"] = new JObject { ["type"] = "settings" },
            ["content"] = new JArray(sections.Select((type, i) => new JObject { ["id"] = $"s{i}", ["type"] = type })),
        }.ToString();

    private static string[] Sections(ThemeSnapshot snapshot, string template) =>
        JObject.Parse(snapshot.Content)["templates"]![template]!["content"]!.Select(x => x.Value<string>("type")).ToArray();

    private sealed class TestThemeSnapshotService(
        IStoreService storeService,
        ISettingsManager settingsManager,
        IThemeStorageFactory themeStorageFactory,
        IThemePublisher themePublisher,
        IPlatformMemoryCache platformMemoryCache,
        IOptions<ThemeSnapshotOptions> options)
        : ThemeSnapshotService(storeService, settingsManager, themeStorageFactory, themePublisher, platformMemoryCache, options)
    {
        public string ResolveLanguagePublic(IList<string> languages, string defaultLanguage, string cultureName) =>
            ResolveLanguage(languages, defaultLanguage, cultureName);
    }

    private sealed class LockProbingHistory(InProcessDistributedLock distributedLock) : IContentHistory
    {
        public List<(ContentHistoryRecord Record, bool LockFree)> Recorded { get; } = [];

        public async Task RecordAsync(string storeId, ContentHistoryRecord record, CancellationToken cancellationToken = default)
        {
            await using var handle = await distributedLock.TryAcquireAsync(ThemeStorage.GetLockResource(storeId), TimeSpan.Zero, cancellationToken);
            Recorded.Add((record, handle != null));
        }

        public Task<ContentHistoryStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ContentVersionPage> ListAsync(string storeId, IList<HistoryItem> items, string cursor, int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> ReadAsync(string storeId, HistoryItem item, string versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
