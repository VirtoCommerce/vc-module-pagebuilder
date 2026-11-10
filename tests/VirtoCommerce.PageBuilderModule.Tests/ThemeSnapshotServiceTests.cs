using System;
using System.Collections.Generic;
using System.IO;
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
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public sealed class ThemeSnapshotServiceTests : IDisposable
{
    // The snapshot cache region is static and keyed by store id, and other classes publish for "s1" in parallel.
    private const string StoreId = "theme-snapshots";
    private const string Product = "default/templates/product.json";
    private const string Header = "default/sections/header-group.json";
    private const string Pointer = "default/snapshots/current.json";

    private readonly FakeBlobStorage _storage = new();
    private readonly FakeSettingsManager _settings = new();
    private readonly TestPlatformMemoryCache _cache = new();
    private readonly InProcessDistributedLock _lock = new();
    private readonly TestThemeSnapshotService _snapshots;
    private readonly ThemePublisher _publisher;
    private readonly SchemaCatalogService _catalogs;
    private readonly Store _store = new() { Id = StoreId, DefaultLanguage = "en-US", Languages = ["en-US", "de-DE"], DynamicProperties = [] };
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
        _storage.Files[Theme(Product)] = Template("product-info");

        Assert.Null(await _snapshots.GetPublishedAsync(StoreId, "en-US", Ct));
        Assert.Equal((ModuleConstants.Settings.StoreSettingsObjectType, StoreId), (_settings.AskedObjectType, _settings.AskedObjectId));
        Assert.Empty(_storage.Written);
    }

    [Fact]
    public async Task GetPublishedAsync_FirstRead_PublishesLiveThemeAndKeepsDrafts()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        _storage.Files[Theme(Product + "-draft")] = Template("product-info", "draft-only");

        var snapshot = await _snapshots.GetPublishedAsync(StoreId, null, Ct);

        Assert.Equal((ModuleConstants.Settings.StoreSettingsObjectType, StoreId), (_settings.AskedObjectType, _settings.AskedObjectId));
        Assert.Equal((1, "en-US"), (snapshot.Publication, snapshot.CultureName));
        Assert.Equal(["product-info"], Sections(snapshot, "product"));
        Assert.True(_storage.Files.ContainsKey(Theme(Product + "-draft")));
    }

    [Fact]
    public async Task PublishAsync_Drafts_WritesLanguagesLiveFilesRecordPointerThenRemovesDrafts()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        _storage.Files[Theme(Product + "-draft")] = Template("product-info", "related-products");
        await _snapshots.GetPublishedAsync(StoreId, null, Ct);
        var catalog = await SaveCatalogAsync("product-info", "related-products");
        _storage.Written.Clear();
        string pointerAtRemoval = null;
        _storage.BeforeRemove = _ => pointerAtRemoval = _storage.Files[Theme(Pointer)];

        var publication = await _publisher.PublishAsync(StoreId, catalog, null, Ct);

        Assert.Equal(2, publication);
        Assert.Equal([Theme("default/snapshots/2/de-DE.json"), Theme("default/snapshots/2/en-US.json")], _storage.Written.Take(2).Order());
        // A cut-off pointer falls back on the record, which marks a complete publication, so it is written after the live files.
        Assert.Equal([Theme(Product), Theme(Record(2)), Theme(Pointer)], _storage.Written.Skip(2));
        Assert.Equal("""{"publication":2}""", pointerAtRemoval);
        Assert.Equal("en-US", (await (await _storageFactory.OpenAsync(StoreId)).GetPublicationRecordAsync(2)).DefaultLanguage);
        Assert.False(_storage.Files.ContainsKey(Theme(Product + "-draft")));
        Assert.Equal(["product-info", "related-products"], Sections(await _snapshots.GetPublishedAsync(StoreId, "de", Ct), "product"));
    }

    [Fact]
    public async Task PublishAsync_Published_WritesThePointerAndTheRecordInTheirStorageFormat()
    {
        _storage.Files[Theme(Product + "-draft")] = Template("product-info");
        await _catalogs.SaveAsync(StoreId, """{ "hash": "abc12345", "sections": { "product-info": {} } }""", Ct);

        await _publisher.PublishAsync(StoreId, "abc12345", null, Ct);

        Assert.Equal("""{"publication":1}""", _storage.Files[Theme(Pointer)]);
        Assert.Equal("""{"defaultLanguage":"en-US"}""", _storage.Files[Theme(Record(1))]);
        Assert.Equal("""{"hash":"abc12345"}""", _storage.Files[Theme("default/catalogs/current.json")]);
        Assert.Equal("""{"hash":"abc12345","sections":{"product-info":{}}}""", _storage.Files[Theme("default/catalogs/abc12345.json")]);
    }

    [Fact]
    public async Task PublishAsync_Published_RecordsHistoryAfterStoreLockIsReleased()
    {
        // GitHub may answer slowly, and the store's theme saves must not wait for it.
        _storage.Files[Theme(Product)] = Template("product-info");
        await _snapshots.GetPublishedAsync(StoreId, null, Ct);
        _storage.Files[Theme(Product + "-draft")] = Template("product-info", "related-products");
        var catalog = await SaveCatalogAsync("product-info", "related-products");
        var history = new LockProbingHistory(_lock);
        var publisher = Publisher(history);

        await publisher.PublishAsync(StoreId, catalog, null, Ct);

        var (record, lockFree) = Assert.Single(history.Recorded);
        Assert.True(lockFree);
        Assert.Equal([$"theme:{Product}"], record.Entries.Select(x => x.Item.ToString()));
    }

    [Fact]
    public async Task PublishAsync_SeveralDrafts_RecordsThemAsOneVersion()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        await _snapshots.GetPublishedAsync(StoreId, null, Ct);
        var header = Template("product-info");
        _storage.Files[Theme(Product + "-draft")] = Template("product-info", "related-products");
        _storage.Files[Theme("default/sections/header-group.json-draft")] = header;
        var catalog = await SaveCatalogAsync("product-info", "related-products");
        var history = new RecordingContentHistory();

        await Publisher(history).PublishAsync(StoreId, catalog, null, Ct);

        var (storeId, record) = Assert.Single(history.Records);
        Assert.Equal(StoreId, storeId);
        Assert.Equal(ModuleConstants.GitHistory.PublishedSummary, record.Description);
        Assert.Equal(["theme:default/sections/header-group.json", $"theme:{Product}"], record.Entries.Select(x => x.Item.ToString()).Order());
        Assert.Equal(header, record.Entries.Single(x => x.Item.Key == "default/sections/header-group.json").Content);
    }

    [Theory]
    [InlineData("default/snapshots/2/de-DE.json")]
    [InlineData(Product)]
    [InlineData("default/snapshots/2/publication.json")]
    [InlineData(Pointer)]
    public async Task PublishAsync_WriteBeforePointerFails_KeepsPreviousPublicationAndDraftsAndRecordsNothing(string failingFile)
    {
        var catalog = await PublishedWithDraftsAsync();
        var versions = await ReadVersionsAsync();
        var history = new RecordingContentHistory();
        var publisher = Publisher(history);
        _storage.BeforeWrite = x => ThrowOn(x, failingFile);

        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(StoreId, catalog, null, Ct));

        var status = await publisher.GetStatusAsync(StoreId, Ct);
        Assert.Equal(1, status.Publication);
        Assert.Equal(["sections/header-group.json", "templates/product.json"], status.Drafts.Order());
        Assert.Equal(Template("product-info"), _storage.Files[Theme(Header)]);
        Assert.Equal(Template("product-info"), _storage.Files[Theme(Product)]);
        Assert.Empty(history.Records);
        Assert.Empty(_storage.Events);
        Assert.Equal(versions, await ReadVersionsAsync());
        Assert.DoesNotContain(_storage.Files.Keys, x => x.StartsWith(Theme("default/snapshots/2/"), StringComparison.OrdinalIgnoreCase));

        _storage.BeforeWrite = null;
        var publication = await publisher.PublishAsync(StoreId, catalog, null, Ct);

        status = await publisher.GetStatusAsync(StoreId, Ct);
        Assert.Equal(publication, status.Publication);
        Assert.Empty(status.Drafts);
        Assert.Equal(Template("related-products"), _storage.Files[Theme(Header)]);
        Assert.Equal(Template("product-info", "related-products"), _storage.Files[Theme(Product)]);
        Assert.Equal(2, Assert.Single(history.Records).Record.Entries.Count);
        Assert.Equal(versions, await ReadVersionsAsync());
    }

    [Fact]
    public async Task PublishAsync_PointerFailsThenCannotBeRead_ServesThePreviousPublication()
    {
        var catalog = await PublishedWithDraftsAsync();
        _storage.BeforeWrite = x => ThrowOn(x, Pointer);
        await Assert.ThrowsAsync<IOException>(() => Publisher(new NoContentHistory()).PublishAsync(StoreId, catalog, null, Ct));
        _storage.BeforeWrite = null;

        _storage.Files[Theme(Pointer)] = "{";

        Assert.Equal(1, await (await _storageFactory.OpenAsync(StoreId)).GetCurrentPublicationAsync());
    }

    [Fact]
    public async Task PublishAsync_FailedPublicationCannotBeRemoved_ThrowsThePublicationFailure()
    {
        var catalog = await PublishedWithDraftsAsync();
        _storage.BeforeWrite = x => ThrowOn(x, Pointer);
        _storage.BeforeRemove = x => ThrowOn(x, Record(2));

        var exception = await Assert.ThrowsAsync<IOException>(() => Publisher(new NoContentHistory()).PublishAsync(StoreId, catalog, null, Ct));

        Assert.Contains(Pointer, exception.Message);
        Assert.Equal(1, (await Publisher(new NoContentHistory()).GetStatusAsync(StoreId, Ct)).Publication);
    }

    [Fact]
    public async Task PublishAsync_PutBackFailsForOneFile_PutsBackTheOthersAndThrowsTheFirstFailure()
    {
        var catalog = await PublishedWithDraftsAsync();
        var publisher = Publisher(new RecordingContentHistory());
        var writes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _storage.BeforeWrite = file =>
        {
            writes[file] = writes.GetValueOrDefault(file) + 1;
            ThrowOn(file, Pointer);
            // The second write of this file is the put-back.
            if (string.Equals(file, Theme(Product), StringComparison.OrdinalIgnoreCase) && writes[file] == 2)
            {
                throw new IOException("the put-back is refused too");
            }
        };

        var exception = await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(StoreId, catalog, null, Ct));

        Assert.Contains(Pointer, exception.Message);
        Assert.Equal(Template("product-info"), _storage.Files[Theme(Header)]);
        Assert.Equal(Template("product-info", "related-products"), _storage.Files[Theme(Product)]);
        Assert.Equal(1, (await publisher.GetStatusAsync(StoreId, Ct)).Publication);
    }

    [Fact]
    public async Task PublishAsync_LockLost_KeepsPreviousPublicationAndDraftsAndRecordsNothing()
    {
        var catalog = await PublishedWithDraftsAsync();
        var history = new RecordingContentHistory();
        var publisher = Publisher(history);
        _lock.LoseLocks = true;

        await Assert.ThrowsAsync<DistributedLockLostException>(() => publisher.PublishAsync(StoreId, catalog, null, Ct));

        var status = await publisher.GetStatusAsync(StoreId, Ct);
        Assert.Equal(1, status.Publication);
        Assert.Equal(["sections/header-group.json", "templates/product.json"], status.Drafts.Order());
        Assert.Equal(Template("product-info"), _storage.Files[Theme(Header)]);
        Assert.Equal(Template("product-info"), _storage.Files[Theme(Product)]);
        Assert.Empty(history.Records);
    }

    [Fact]
    public async Task PublishAsync_DraftRemovalFails_PublicationStandsAndLeftDraftEqualsLiveFile()
    {
        var catalog = await PublishedWithDraftsAsync();
        var versions = await ReadVersionsAsync();
        var history = new RecordingContentHistory();
        var publisher = Publisher(history);
        _storage.BeforeRemove = x => ThrowOn(x, Product + "-draft");

        var publication = await publisher.PublishAsync(StoreId, catalog, null, Ct);

        var status = await publisher.GetStatusAsync(StoreId, Ct);
        Assert.Equal((2, 2), (publication, status.Publication));
        Assert.Equal(["templates/product.json"], status.Drafts);
        Assert.Equal(_storage.Files[Theme(Product)], _storage.Files[Theme(Product + "-draft")]);
        Assert.Equal([$"theme:{Header}", $"theme:{Product}"], Assert.Single(history.Records).Record.Entries.Select(x => x.Item.ToString()).Order());
        Assert.Single(_storage.Events);
        Assert.Equal(versions, await ReadVersionsAsync());

        _storage.BeforeRemove = null;
        Assert.Equal(3, await publisher.PublishAsync(StoreId, catalog, null, Ct));
        Assert.Empty((await publisher.GetStatusAsync(StoreId, Ct)).Drafts);
        Assert.Equal(Template("product-info", "related-products"), _storage.Files[Theme(Product)]);
        Assert.Equal(versions, await ReadVersionsAsync());
    }

    [Fact]
    public async Task GetStatusAsync_BeforeFirstPublication_ListsDraftsWithoutPublication()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        _storage.Files[Theme("default/templates/product.b2c.json-draft")] = Template("product-info");
        _storage.Files[Theme("default/sections/header-group.json-draft")] = """{ "content": [] }""";
        _storage.Files[Theme("default/config/settings_data.json")] = "{}";

        var status = await _publisher.GetStatusAsync(StoreId, Ct);

        Assert.Null(status.Publication);
        Assert.Equal(["sections/header-group.json", "templates/product.b2c.json"], status.Drafts.Order());
    }

    [Fact]
    public async Task GetStatusAsync_AfterPublication_ReturnsPublicationAndNoDrafts()
    {
        _storage.Files[Theme(Product + "-draft")] = Template("product-info");
        _storage.Files[Theme("default/config/template_rules.json-draft")] = """{ "version": 1 }""";
        var catalog = await SaveCatalogAsync("product-info");
        Assert.Equal(["config/template_rules.json", "templates/product.json"], (await _publisher.GetStatusAsync(StoreId, Ct)).Drafts.Order());

        var publication = await _publisher.PublishAsync(StoreId, catalog, null, Ct);

        var status = await _publisher.GetStatusAsync(StoreId, Ct);
        Assert.Equal(publication, status.Publication);
        Assert.Empty(status.Drafts);
    }

    [Fact]
    public async Task ValidateFilesAsync_FilesOfTypesCatalogHas_Passes()
    {
        var catalog = await SaveCatalogAsync("product-info", "site-header");

        await _publisher.ValidateFilesAsync(StoreId, catalog, new Dictionary<string, JToken>
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
            _publisher.ValidateFilesAsync(StoreId, catalog, new Dictionary<string, JToken> { [file] = JToken.Parse(Template("product-info", "carousel")) }, Ct));

        Assert.StartsWith($"{file}:", failure.Errors.Single());
        Assert.Contains("'carousel'", failure.Errors.Single());
    }

    [Fact]
    public async Task ValidateFilesAsync_UnknownCatalogHash_Throws()
    {
        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() =>
            _publisher.ValidateFilesAsync(StoreId, "missing123", new Dictionary<string, JToken> { ["templates/product.json"] = JToken.Parse(Template("product-info")) }, Ct));

        Assert.Contains("missing123", failure.Errors.Single());
    }

    [Fact]
    public async Task PublishAsync_CatalogRefusesDraft_WritesNothing()
    {
        await _catalogs.SaveAsync(StoreId, """{ "hash": "abc12345", "sections": { "product-info": {} }, "blocks": {}, "shared": {} }""", Ct);
        _storage.Files[Theme(Product + "-draft")] = Template("product-info", "carousel");
        _storage.Written.Clear();

        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync(StoreId, "abc12345", null, Ct));

        Assert.Contains("'carousel'", failure.Errors.Single());
        Assert.Empty(_storage.Written);
        Assert.True(_storage.Files.ContainsKey(Theme(Product + "-draft")));
    }

    [Fact]
    public async Task PublishAsync_DraftWithValueTheCatalogLacks_ThrowsAndWritesNothing()
    {
        await _catalogs.SaveAsync(StoreId, """
            {
              "hash": "abc12345",
              "sections": { "product-info": { "settings": [ { "id": "background", "type": "color-token" } ] } },
              "paletteTokens": { "light": [ "color_primary_500" ], "dark": [] }
            }
            """, Ct);
        _storage.Files[Theme(Product + "-draft")] = """{ "content": [ { "id": "s0", "type": "product-info", "background": "#ff0000" } ] }""";
        _storage.Written.Clear();

        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync(StoreId, "abc12345", null, Ct));

        Assert.Equal("templates/product.json: 's0' has a value in 'background' the storefront does not have, '#ff0000'", failure.Errors.Single());
        Assert.Empty(_storage.Written);
        Assert.True(_storage.Files.ContainsKey(Theme(Product + "-draft")));
    }

    [Fact]
    public async Task PublishAsync_NoCatalogHash_ThrowsAndWritesNothing()
    {
        _storage.Files[Theme(Product + "-draft")] = Template("product-info");

        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync(StoreId, null, null, Ct));

        Assert.Contains("catalog", failure.Errors.Single());
        Assert.Empty(_storage.Written);
    }

    [Fact]
    public async Task GetPublishedAsync_FirstPublication_FindsMenusThroughCurrentCatalog()
    {
        await _catalogs.SaveAsync(StoreId, """
            { "hash": "abc12345", "sections": { "site-header": { "settings": [ { "id": "menu", "type": "menu" } ] } } }
            """, Ct);
        _storage.Files[Theme("default/sections/header-group.json")] = """
            { "content": [ { "id": "h", "type": "site-header", "menu": "main-menu" } ] }
            """;

        var snapshot = await _snapshots.GetPublishedAsync(StoreId, null, Ct);

        Assert.Equal(["main-menu"], snapshot.Menus);
        // Validated against no catalog because the live files may predate the storefront's schemas.
        Assert.Equal(JTokenType.Null, JObject.Parse(snapshot.Content)["catalogHash"]!.Type);
    }

    [Fact]
    public async Task PublishAsync_UnknownCatalogHash_Throws()
    {
        var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync(StoreId, "missing123", null, Ct));

        Assert.Contains("missing123", failure.Errors.Single());
    }

    [Fact]
    public async Task GetPublishedAsync_AfterPublication_ReturnsNewPublication()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        Assert.Equal(1, (await _snapshots.GetPublishedAsync(StoreId, "en-US", Ct)).Publication);

        _storage.Files[Theme(Product + "-draft")] = Template("product-info", "related-products");
        await _publisher.PublishAsync(StoreId, await SaveCatalogAsync("product-info", "related-products"), null, Ct);

        Assert.Equal(2, (await _snapshots.GetPublishedAsync(StoreId, "en-US", Ct)).Publication);
    }

    [Fact]
    public async Task PublishAsync_ThemeSaveRunning_WaitsForStoreLock()
    {
        _storage.Files[Theme(Product + "-draft")] = Template("product-info");
        var catalog = await SaveCatalogAsync("product-info");
        var save = await _lock.AcquireAsync(ThemeStorage.GetLockResource(StoreId), cancellationToken: Ct);

        var publication = _publisher.PublishAsync(StoreId, catalog, null, Ct);

        Assert.Equal(ThemeStorage.GetLockResource(StoreId), await _lock.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.DoesNotContain(_storage.Written, x => x.StartsWith(Theme("default/snapshots/"), StringComparison.Ordinal));

        save.Dispose();
        Assert.Equal(1, await publication);
    }

    [Fact]
    public async Task GetPublishedAsync_CachingOff_ReadsSnapshotAgain()
    {
        using var uncached = new TestPlatformMemoryCache(() => new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromTicks(1) });
        var snapshots = Snapshots(uncached);
        _storage.Files[Theme(Product)] = Template("product-info");
        await snapshots.GetPublishedAsync(StoreId, "en-US", Ct);
        // Read once more, because the first read published, which resets what it would have kept.
        var read = await snapshots.GetPublishedAsync(StoreId, "en-US", Ct);

        _storage.Files[Theme("default/snapshots/1/en-US.json")] = read.Content.Replace("product-info", "related-products");

        Assert.Contains("related-products", (await snapshots.GetPublishedAsync(StoreId, "en-US", Ct)).Content);
    }

    [Fact]
    public async Task GetPublishedAsync_UnpublishableLiveFiles_CachesFailureUntilReset()
    {
        const string header = "default/sections/header-group.json";
        _storage.Files[Theme(header)] = """{ "content": [ { "type": "banner" } ] }""";
        await Assert.ThrowsAsync<ThemeValidationException>(() => _snapshots.GetPublishedAsync(StoreId, "en-US", Ct));

        _lock.Hold();
        await Assert.ThrowsAsync<ThemeValidationException>(() => _snapshots.GetPublishedAsync(StoreId, "de-DE", Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct));
        _lock.Release();

        _storage.Files[Theme(header)] = """{ "content": [ { "id": "h", "type": "banner" } ] }""";
        ThemeSnapshotCacheRegion.ExpireTokenForKey(StoreId);
        Assert.Equal(1, (await _snapshots.GetPublishedAsync(StoreId, "en-US", Ct)).Publication);
    }

    [Fact]
    public async Task GetPublishedAsync_TruncatedPointer_ReturnsHighestCompletePublication()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        await _snapshots.GetPublishedAsync(StoreId, null, Ct);
        _storage.Files[Theme(Product + "-draft")] = Template("product-info", "related-products");
        await _publisher.PublishAsync(StoreId, await SaveCatalogAsync("product-info", "related-products"), null, Ct);
        _storage.Files[Theme(Pointer)] = "{ \"publicat";
        using var emptyCache = new TestPlatformMemoryCache();

        var snapshot = await Snapshots(emptyCache).GetPublishedAsync(StoreId, null, Ct);

        Assert.Equal(2, snapshot.Publication);
    }

    [Fact]
    public async Task GetPublishedAsync_TruncatedPointerAndRecord_SkipsIncompletePublication()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        await _snapshots.GetPublishedAsync(StoreId, null, Ct);
        _storage.Files[Theme("default/snapshots/2/de-DE.json")] = "{}";
        _storage.Files[Theme("default/snapshots/2/en-US.json")] = "{}";
        _storage.Files[Theme(Record(2))] = "{\"defaultLangu";
        _storage.Files[Theme(Pointer)] = "{ \"publicat";
        using var emptyCache = new TestPlatformMemoryCache();

        var snapshot = await Snapshots(emptyCache).GetPublishedAsync(StoreId, null, Ct);

        Assert.Equal(1, snapshot.Publication);
    }

    [Fact]
    public async Task GetPublishedAsync_StoreDefaultWithoutFile_ReturnsPublicationDefault()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        await _snapshots.GetPublishedAsync(StoreId, null, Ct);
        _store.Languages = ["en-US", "de-DE", "fr-FR"];
        _store.DefaultLanguage = "fr-FR";

        var snapshot = await _snapshots.GetPublishedAsync(StoreId, null, Ct);

        Assert.Equal((1, "en-US"), (snapshot.Publication, snapshot.CultureName));
    }

    [Fact]
    public async Task GetPublishedAsync_TruncatedPointerAfterDefaultLanguageChanged_ReturnsCompletePublication()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        await _snapshots.GetPublishedAsync(StoreId, null, Ct);
        _store.Languages = ["en-US", "de-DE", "fr-FR"];
        _store.DefaultLanguage = "fr-FR";
        _storage.Files[Theme(Pointer)] = "{ \"publicat";

        var snapshot = await _snapshots.GetPublishedAsync(StoreId, null, Ct);

        Assert.Equal((1, "en-US"), (snapshot.Publication, snapshot.CultureName));
    }

    [Fact]
    public async Task GetPublishedAsync_TruncatedPointerWithoutCompletePublication_PublishesLiveThemeAgain()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        _storage.Files[Theme("default/snapshots/1/de-DE.json")] = "{}";
        _storage.Files[Theme(Pointer)] = "{ \"publicat";

        var snapshot = await _snapshots.GetPublishedAsync(StoreId, null, Ct);

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

    private Task<string> SaveCatalogAsync(params string[] sections) =>
        _catalogs.SaveAsync(StoreId, new JObject
        {
            ["hash"] = "catalog-" + string.Join('-', sections),
            ["sections"] = new JObject(sections.Select(x => new JProperty(x, new JObject()))),
        }.ToString(), Ct);

    [Fact]
    public async Task PublishAsync_LockLostWhileWritingLiveFiles_StopsAndPutsNothingBack()
    {
        var catalog = await PublishedWithDraftsAsync();
        var publisher = Publisher(new RecordingContentHistory());
        var liveWrites = new List<string>();
        _storage.BeforeWrite = file =>
        {
            if (IsLiveFile(file))
            {
                liveWrites.Add(file);
                _lock.LoseHeldLocks();
            }
        };

        await Assert.ThrowsAnyAsync<Exception>(() => publisher.PublishAsync(StoreId, catalog, null, Ct));

        var written = Assert.Single(liveWrites);
        Assert.Equal(_storage.Files[written + "-draft"], _storage.Files[written]);
        var status = await publisher.GetStatusAsync(StoreId, Ct);
        Assert.Equal(1, status.Publication);
        Assert.Equal(2, status.Drafts.Count);
    }

    [Fact]
    public async Task PublishAsync_RequestAbortedWhileWritingLiveFiles_PutsThemBack()
    {
        var catalog = await PublishedWithDraftsAsync();
        var publisher = Publisher(new RecordingContentHistory());
        using var request = new CancellationTokenSource();
        _storage.BeforeWrite = file =>
        {
            if (IsLiveFile(file))
            {
                request.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync(StoreId, catalog, null, request.Token));

        Assert.Equal(Template("product-info"), _storage.Files[Theme(Header)]);
        Assert.Equal(Template("product-info"), _storage.Files[Theme(Product)]);
    }

    [Fact]
    public async Task GetStatusAsync_DraftWithNothingLive_IsListedAsNew()
    {
        await PublishedWithDraftsAsync();
        _storage.Files[Theme("default/templates/product.sale.json-draft")] = Template("product-info");

        var status = await _publisher.GetStatusAsync(StoreId, Ct);

        Assert.Equal(3, status.Drafts.Count);
        Assert.Equal(["templates/product.sale.json"], status.NewDrafts);
    }

    [Fact]
    public async Task GetPublishedAsync_CacheLifetimeZero_StillAnswers()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        var snapshots = new TestThemeSnapshotService(_stores, _settings, _storageFactory, _publisher, _cache,
            Options.Create(new ThemeSnapshotOptions { CacheAbsoluteExpiration = TimeSpan.Zero }));

        Assert.NotNull(await snapshots.GetPublishedAsync(StoreId, null, Ct));
    }

    private static bool IsLiveFile(string file) =>
        string.Equals(file, Theme(Header), StringComparison.OrdinalIgnoreCase) || string.Equals(file, Theme(Product), StringComparison.OrdinalIgnoreCase);

    private async Task<string> PublishedWithDraftsAsync()
    {
        _storage.Files[Theme(Product)] = Template("product-info");
        _storage.Files[Theme(Header)] = Template("product-info");
        await _snapshots.GetPublishedAsync(StoreId, null, Ct);
        _storage.Files[Theme(Product + "-draft")] = Template("product-info", "related-products");
        _storage.Files[Theme(Header + "-draft")] = Template("related-products");
        var catalog = await SaveCatalogAsync("product-info", "related-products");
        _storage.Events.Clear();

        return catalog;
    }

    private async Task<string[]> ReadVersionsAsync()
    {
        var drafts = new ThemeDraftService(_storageFactory, _lock, _storage, NullLogger<ThemeDraftService>.Instance);

        return [(await drafts.ReadAsync(StoreId, Product, Ct)).ETag, (await drafts.ReadAsync(StoreId, Header, Ct)).ETag];
    }

    private static void ThrowOn(string file, string failingFile)
    {
        if (string.Equals(file, Theme(failingFile), StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"{file} cannot be changed");
        }
    }

    private ThemePublisher Publisher(IContentHistory history) =>
        new(_storageFactory, new ThemeSnapshotBuilder(), new ThemeValidator(), _stores, _catalogs, _lock, _storage, history,
            Options.Create(new ThemeSnapshotOptions()), NullLogger<ThemePublisher>.Instance);

    private TestThemeSnapshotService Snapshots(TestPlatformMemoryCache cache) =>
        new(_stores, _settings, _storageFactory, _publisher, cache, Options.Create(new ThemeSnapshotOptions()));

    private static string Theme(string path) => $"themes/{StoreId}/{path}";

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
