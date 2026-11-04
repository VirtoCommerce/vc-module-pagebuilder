using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public sealed class ThemeSnapshotServiceTests : IDisposable
    {
        private const string Product = "default/templates/product.json";
        private const string Pointer = "default/snapshots/current.json";

        private readonly FakeThemeStorage _storage = new();
        private readonly MemorySettingsManager _settings = new();
        private readonly TestPlatformMemoryCache _cache = new();
        private readonly InProcessDistributedLock _lock = new();
        private readonly ThemeSnapshotService _snapshots;
        private readonly ThemePublisher _publisher;
        private readonly SchemaCatalogService _catalogs;
        private readonly FakeStoreService _stores;
        private readonly ThemeStorageFactory _storageFactory;

        public ThemeSnapshotServiceTests()
        {
            _stores = new FakeStoreService(new Store { Id = "s1", DefaultLanguage = "en-US", Languages = ["en-US", "de-DE"], DynamicProperties = [] });
            _storageFactory = new ThemeStorageFactory(_stores, _storage, _storage, _storage);
            _catalogs = new SchemaCatalogService(_storageFactory);
            _publisher = new ThemePublisher(_storageFactory, _stores, _catalogs, _lock, new NoEvents(), NullLogger<ThemePublisher>.Instance);
            _snapshots = new ThemeSnapshotService(_stores, _settings, _storageFactory, _publisher, _cache);
            _settings.Values[ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder.Name] = true;
        }

        private static System.Threading.CancellationToken Ct => TestContext.Current.CancellationToken;

        public void Dispose() => _cache.Dispose();

        [Fact]
        public async Task A_store_with_the_New_Page_Builder_off_has_no_snapshot()
        {
            _settings.Values[ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder.Name] = false;
            _storage.Files[Product] = Template("product-info");

            Assert.Null(await _snapshots.GetPublishedAsync("s1", "en-US", Ct));
            Assert.Empty(_storage.Written);
        }

        [Fact]
        public async Task The_first_read_publishes_the_live_theme_and_leaves_the_drafts_alone()
        {
            _storage.Files[Product] = Template("product-info");
            _storage.Files[Product + "-draft"] = Template("product-info", "draft-only");

            var snapshot = await _snapshots.GetPublishedAsync("s1", null, Ct);

            Assert.Equal((1, "en-US"), (snapshot.Publication, snapshot.CultureName));
            Assert.Equal(["product-info"], Sections(snapshot, "product"));
            Assert.True(_storage.Files.ContainsKey(Product + "-draft"));
        }

        [Fact]
        public async Task Publishing_writes_every_language_the_default_one_last_then_the_pointer_then_puts_the_drafts_live()
        {
            _storage.Files[Product] = Template("product-info");
            _storage.Files[Product + "-draft"] = Template("product-info", "related-products");
            await _snapshots.GetPublishedAsync("s1", null, Ct);
            var catalog = await Catalog("product-info", "related-products");
            _storage.Written.Clear();

            var publication = await _publisher.PublishAsync("s1", catalog, null, Ct);

            Assert.Equal(2, publication);
            // the default language's file marks a complete publication (a pointer cut off falls back on it)
            Assert.Equal(
                [
                    "default/snapshots/2/de-DE.json",
                    "default/snapshots/2/en-US.json",
                    Pointer,
                    Product,
                ],
                _storage.Written);
            Assert.False(_storage.Files.ContainsKey(Product + "-draft"));
            Assert.Equal(["product-info", "related-products"], Sections(await _snapshots.GetPublishedAsync("s1", "de", Ct), "product"));
        }

        [Fact]
        public async Task A_publication_the_catalog_refuses_writes_nothing()
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
        public async Task A_publication_names_the_catalog_it_was_previewed_with()
        {
            _storage.Files[Product + "-draft"] = Template("product-info");

            var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync("s1", null, null, Ct));

            Assert.Contains("catalog", failure.Errors.Single());
            Assert.Empty(_storage.Written);
        }

        [Fact]
        public async Task The_first_publication_finds_the_menus_through_the_current_catalog()
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
        public async Task A_publication_names_a_catalog_the_server_has()
        {
            var failure = await Assert.ThrowsAsync<ThemeValidationException>(() => _publisher.PublishAsync("s1", "missing123", null, Ct));

            Assert.Contains("missing123", failure.Errors.Single());
        }

        [Fact]
        public async Task A_cached_snapshot_is_reset_by_the_next_publication()
        {
            _storage.Files[Product] = Template("product-info");
            Assert.Equal(1, (await _snapshots.GetPublishedAsync("s1", "en-US", Ct)).Publication);

            _storage.Files[Product + "-draft"] = Template("product-info", "related-products");
            await _publisher.PublishAsync("s1", await Catalog("product-info", "related-products"), null, Ct);

            Assert.Equal(2, (await _snapshots.GetPublishedAsync("s1", "en-US", Ct)).Publication);
        }

        [Fact]
        public async Task A_pointer_cut_off_reads_as_the_highest_complete_publication()
        {
            _storage.Files[Product] = Template("product-info");
            await _snapshots.GetPublishedAsync("s1", null, Ct);
            _storage.Files[Product + "-draft"] = Template("product-info", "related-products");
            await _publisher.PublishAsync("s1", await Catalog("product-info", "related-products"), null, Ct);
            _storage.Files[Pointer] = "{ \"publicat";
            using var emptyCache = new TestPlatformMemoryCache();

            var snapshot = await new ThemeSnapshotService(_stores, _settings, _storageFactory, _publisher, emptyCache).GetPublishedAsync("s1", null, Ct);

            Assert.Equal(2, snapshot.Publication);
        }

        [Fact]
        public async Task A_pointer_cut_off_skips_a_publication_cut_off_in_its_last_file()
        {
            _storage.Files[Product] = Template("product-info");
            await _snapshots.GetPublishedAsync("s1", null, Ct);
            // publication 2 stopped while writing the default language, the file written last
            _storage.Files["default/snapshots/2/de-DE.json"] = "{}";
            _storage.Files["default/snapshots/2/en-US.json"] = "{\"format\":1,\"publi";
            _storage.Files[Pointer] = "{ \"publicat";
            using var emptyCache = new TestPlatformMemoryCache();

            var snapshot = await new ThemeSnapshotService(_stores, _settings, _storageFactory, _publisher, emptyCache).GetPublishedAsync("s1", null, Ct);

            Assert.Equal(1, snapshot.Publication);
        }

        [Theory]
        [InlineData("de-DE", "de-DE")]
        [InlineData("DE-de", "de-DE")]
        [InlineData("de", "de-DE")]
        [InlineData("en-GB", "en-US")]
        [InlineData("fr-FR", "en-US")]
        [InlineData(null, "en-US")]
        [InlineData("", "en-US")]
        public void The_language_served_is_the_store_language_closest_to_the_one_asked_for(string requested, string served)
        {
            Assert.Equal(served, ThemeSnapshotService.ResolveLanguage(["en-US", "de-DE", "de-AT"], "en-US", requested));
        }

        [Fact]
        public void Among_store_languages_of_the_same_language_the_default_one_wins()
        {
            Assert.Equal("de-AT", ThemeSnapshotService.ResolveLanguage(["de-DE", "de-AT"], "de-AT", "de"));
        }

        [Fact]
        public async Task A_catalog_keeps_the_first_content_of_its_hash_and_becomes_the_current_one()
        {
            await _catalogs.SaveAsync("s1", """{ "hash": "abc12345", "sections": { "a": {} } }""", Ct);
            await _catalogs.SaveAsync("s1", """{ "hash": "abc12345", "sections": { "b": {} } }""", Ct);

            Assert.NotNull(JObject.Parse(await _catalogs.GetAsync("s1", "abc12345", Ct))["sections"]!["a"]);
            Assert.Equal(await _catalogs.GetAsync("s1", "abc12345", Ct), await _catalogs.GetAsync("s1", null, Ct));
            await Assert.ThrowsAsync<ArgumentException>(() => _catalogs.SaveAsync("s1", """{ "hash": "../x" }""", Ct));
        }

        /// <summary>Keeps a storefront catalog that has these section types; its hash.</summary>
        private Task<string> Catalog(params string[] sections) =>
            _catalogs.SaveAsync("s1", new JObject
            {
                ["hash"] = "catalog-" + string.Join('-', sections),
                ["sections"] = new JObject(sections.Select(x => new JProperty(x, new JObject()))),
            }.ToString(), Ct);

        private static string Template(params string[] sections) =>
            new JObject
            {
                ["settings"] = new JObject { ["type"] = "settings" },
                ["content"] = new JArray(sections.Select((type, i) => new JObject { ["id"] = $"s{i}", ["type"] = type })),
            }.ToString();

        private static string[] Sections(ThemeSnapshot snapshot, string template) =>
            JObject.Parse(snapshot.Content)["templates"]![template]!["content"]!.Select(x => x.Value<string>("type")).ToArray();

        private sealed class NoEvents : IEventPublisher
        {
            public Task Publish<T>(T @event, System.Threading.CancellationToken cancellationToken = default) where T : IEvent => Task.CompletedTask;
        }
    }
}
