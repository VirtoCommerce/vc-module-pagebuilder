using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.ContentProviders;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Pages.Core.Models;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.GenericCrud;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderContentProviderSharedComponentChangeTests
{
    [Fact]
    public async Task SearchChangesAsync_UsesEffectiveComponentDateForWindowPagingAndChangeDate()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var provider = CreateProvider(database, cache);
        var criteria = new PageChangesSearchCriteria
        {
            StartDate = WindowStart,
            EndDate = WindowEnd,
            Take = 1,
        };

        var firstPage = await provider.SearchChangesAsync(criteria);
        criteria.Skip = 1;
        var secondPage = await provider.SearchChangesAsync(criteria);
        var changes = firstPage.Results.Concat(secondPage.Results).ToDictionary(x => x.DocumentId);

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(2, secondPage.TotalCount);
        Assert.Equal([PageWithComponentId, PageWithMultipleComponentsId], changes.Keys.Order());
        Assert.Equal(ComponentChangeDate, changes[PageWithComponentId].ChangeDate);
        Assert.Equal(LatestComponentChangeDate, changes[PageWithMultipleComponentsId].ChangeDate);
        Assert.DoesNotContain(PageWithFutureComponentId, changes.Keys);
    }

    [Fact]
    public async Task SearchChangesAsync_PageWithoutReferencesUsesItsOwnDate()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var provider = CreateProvider(database, cache);

        var result = await provider.SearchChangesAsync(new PageChangesSearchCriteria
        {
            StartDate = OwnPageChangeDate.AddTicks(-1),
            EndDate = OwnPageChangeDate,
            Take = 10,
        });

        var change = Assert.Single(result.Results);
        Assert.Equal(PageWithoutComponentsId, change.DocumentId);
        Assert.Equal(OwnPageChangeDate, change.ChangeDate);
    }

    [Fact]
    public async Task GetByIdsAsync_MaterializesSharedComponentWithoutChangingRawPageContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var provider = CreateProvider(database, cache);

        var document = Assert.Single(await provider.GetByIdsAsync([PageWithComponentId]));
        var sections = System.Text.Json.Nodes.JsonNode.Parse(document.Content)["content"].AsArray();

        Assert.Equal("shared-hero", Assert.Single(sections)["type"].GetValue<string>());
        Assert.DoesNotContain("componentRef", document.Content);
        Assert.Equal(
            RawPageWithComponentContent,
            await database.LoadRawContentAsync(PageWithComponentId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetByIdsAsync_MissingSharedComponentOmitsOnlyItsPlacement()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var provider = CreateProvider(database, cache);

        var document = Assert.Single(await provider.GetByIdsAsync([PageWithMissingComponentId]));
        var sections = System.Text.Json.Nodes.JsonNode.Parse(document.Content)["content"].AsArray();

        Assert.Equal("ordinary", Assert.Single(sections)["id"].GetValue<string>());
        Assert.Equal(
            RawPageWithMissingComponentContent,
            await database.LoadRawContentAsync(PageWithMissingComponentId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ComponentContentUpdate_ProducesPageChangeAndUpdatedMaterializedDocument()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        var contentService = new PageBuilderSharedComponentContentService(
            database.RepositoryFactory,
            new NoopEventPublisher(),
            new PageBuilderSharedComponentAssetReferenceIndexService());
        var started = DateTime.UtcNow;

        await contentService.SaveContentAsync(
            ComponentId,
            UpdatedSharedComponentContent,
            TestContext.Current.CancellationToken);
        var finished = DateTime.UtcNow;

        using var cache = new TestPlatformMemoryCache();
        var provider = CreateProvider(database, cache, contentService);
        var changes = await provider.SearchChangesAsync(new PageChangesSearchCriteria
        {
            StartDate = started,
            EndDate = finished,
            Take = 10,
        });
        var change = Assert.Single(changes.Results);
        var document = Assert.Single(await provider.GetByIdsAsync([PageWithComponentId]));

        Assert.Equal(PageWithComponentId, change.DocumentId);
        Assert.InRange(change.ChangeDate, started, finished);
        Assert.Contains("Updated shared title", document.Content);
        Assert.DoesNotContain("componentRef", document.Content);
        Assert.Equal(
            RawPageWithComponentContent,
            await database.LoadRawContentAsync(PageWithComponentId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchChangesAsync_FallsBackToPageDateWhenEffectiveDateIsMissing()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        // The effective-date lookup skips pages without an id, so the provider must tolerate a page that has
        // no entry instead of aborting the whole indexation cycle on a dictionary miss.
        var provider = CreateProvider(database, cache, changeService: new EmptyPageChangeService());

        var result = await provider.SearchChangesAsync(new PageChangesSearchCriteria
        {
            StartDate = OwnPageChangeDate.AddTicks(-1),
            EndDate = OwnPageChangeDate,
            Take = 10,
        });

        var change = Assert.Single(result.Results);
        Assert.Equal(PageWithoutComponentsId, change.DocumentId);
        Assert.Equal(OwnPageChangeDate, change.ChangeDate);
    }

    [Fact]
    public async Task GetByIdsAsync_MalformedMarkerSkipsOnlyItsOwnPage()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var provider = CreateProvider(database, cache);

        var documents = await provider.GetByIdsAsync([PageWithMalformedMarkerId, PageWithComponentId]);

        var document = Assert.Single(documents);
        Assert.Equal(PageWithComponentId, document.Id);
        Assert.Contains("Shared title", document.Content);
    }

    [Fact]
    public async Task GetByIdsAsync_LoadsSharedComponentContentsOncePerBatch()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var contentService = new CountingContentService(new PageBuilderSharedComponentContentService(
            database.RepositoryFactory,
            new NoopEventPublisher(),
            new PageBuilderSharedComponentAssetReferenceIndexService()));
        var provider = CreateProvider(database, cache, contentService);

        var documents = await provider.GetByIdsAsync([PageWithComponentId, PageWithSameComponentId]);

        Assert.Equal(2, documents.Count);
        Assert.All(documents, document => Assert.Contains("Shared title", document.Content));
        Assert.Equal(1, contentService.LoadContentsCallCount);
    }

    private static PageBuilderContentProvider CreateProvider(
        TestDatabase database,
        IPlatformMemoryCache cache,
        IPageBuilderSharedComponentContentService contentService = null,
        Func<IPageBuilderModuleRepository> repositoryFactory = null,
        IPageBuilderPageChangeService changeService = null)
    {
        repositoryFactory ??= database.RepositoryFactory;
        var pageService = new PageBuilderPageService(
            repositoryFactory,
            cache,
            new NoopEventPublisher());
        var searchService = new PageBuilderPageSearchService(
            repositoryFactory,
            cache,
            pageService,
            Options.Create(new CrudOptions()));
        contentService ??= new PageBuilderSharedComponentContentService(
            database.RepositoryFactory,
            new NoopEventPublisher(),
            new PageBuilderSharedComponentAssetReferenceIndexService());
        var resolver = new PageBuilderSharedComponentResolver(
            contentService,
            NullLogger<PageBuilderSharedComponentResolver>.Instance);

        return new PageBuilderContentProvider(
            searchService,
            changeService ?? new PageBuilderPageChangeService(repositoryFactory),
            new TestGroupedPageService(database),
            resolver,
            NullLogger<PageBuilderContentProvider>.Instance);
    }

    private sealed class NoopEventPublisher : IEventPublisher
    {
        public Task Publish<T>(T @event, CancellationToken cancellationToken = default)
            where T : IEvent
        {
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyPageChangeService : IPageBuilderPageChangeService
    {
        public Task<IReadOnlyDictionary<string, DateTime>> GetEffectiveChangeDatesAsync(
            IEnumerable<PageBuilderPage> pages,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, DateTime> result =
                new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }
    }

    private sealed class CountingContentService(IPageBuilderSharedComponentContentService inner)
        : IPageBuilderSharedComponentContentService
    {
        public int LoadContentsCallCount { get; private set; }

        public Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
            IEnumerable<string> sharedComponentIds,
            CancellationToken cancellationToken = default)
        {
            LoadContentsCallCount++;
            return inner.LoadContentsAsync(sharedComponentIds, cancellationToken);
        }

        public Task<string> LoadContentAsync(string sharedComponentId, CancellationToken cancellationToken = default) =>
            inner.LoadContentAsync(sharedComponentId, cancellationToken);

        public Task<string> TryLoadContentAsync(
            PageBuilderSharedComponent expectedComponent,
            CancellationToken cancellationToken = default) =>
            inner.TryLoadContentAsync(expectedComponent, cancellationToken);

        public Task SaveContentAsync(
            string sharedComponentId,
            string content,
            CancellationToken cancellationToken = default) =>
            inner.SaveContentAsync(sharedComponentId, content, cancellationToken);

        public Task<bool> TrySaveContentAsync(
            PageBuilderSharedComponent expectedComponent,
            string content,
            CancellationToken cancellationToken = default) =>
            inner.TrySaveContentAsync(expectedComponent, content, cancellationToken);
    }

    private sealed class TestPlatformMemoryCache : IPlatformMemoryCache
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        public ICacheEntry CreateEntry(object key) => _cache.CreateEntry(key);
        public void Remove(object key) => _cache.Remove(key);
        public bool TryGetValue(object key, out object value) => _cache.TryGetValue(key, out value);
        public MemoryCacheEntryOptions GetDefaultCacheEntryOptions() => new();
        public void Dispose() => _cache.Dispose();
    }

    private sealed class TestGroupedPageService(TestDatabase database) : IGroupedPageService
    {
        public Task<IList<GroupedPageBuilderPage>> GetAsync(
            IList<string> ids,
            string responseGroup = null,
            bool clone = true)
        {
            IList<GroupedPageBuilderPage> result = ids.Contains(GroupId)
                ?
                [
                    new GroupedPageBuilderPage
                    {
                        Id = GroupId,
                        StoreId = StoreId,
                        CultureName = "en-US",
                        Name = "Pages",
                        Permalink = "/pages",
                        Pages = [],
                    },
                ]
                : [];
            return Task.FromResult(result);
        }

        public Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default) =>
            database.LoadRawContentAsync(pageId, cancellationToken);

        public Task SaveChangesAsync(IList<GroupedPageBuilderPage> models) => throw new NotSupportedException();
        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
        public Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> LoadContentToStreamAsync(
            string pageId,
            Stream stream,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveStreamAsContentAsync(
            string pageId,
            Stream stream,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CopyPageContentAsync(
            string sourcePageId,
            string targetPageId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryDeleteEmptyDraftAsync(
            string pageId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _anchorConnection;

        private TestDatabase(string connectionString, SqliteConnection anchorConnection)
        {
            ConnectionString = connectionString;
            _anchorConnection = anchorConnection;
        }

        private string ConnectionString { get; }

        public Func<IPageBuilderModuleRepository> RepositoryFactory =>
            () => new PageBuilderModuleRepository(CreateContext());

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"page-builder-effective-change-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
            }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new TestDatabase(connectionString, anchorConnection);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return database;
        }

        public PageBuilderModuleDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
                .UseSqlite(ConnectionString)
                .Options;
            return new PageBuilderModuleDbContext(options);
        }

        public async Task SeedAsync()
        {
            await using var context = CreateContext();
            context.Add(new GroupedPageBuilderPageEntity
            {
                Id = GroupId,
                StoreId = StoreId,
                CultureName = "en-US",
                Name = "Pages",
                CreatedDate = OldDate,
            });
            context.AddRange(
                Page(PageWithComponentId, OldDate, RawPageWithComponentContent),
                Page(PageWithFutureComponentId, OldDate.AddTicks(1)),
                Page(PageWithMultipleComponentsId, OldDate.AddTicks(2)),
                Page(PageWithMissingComponentId, OldDate.AddTicks(3), RawPageWithMissingComponentContent),
                Page(PageWithoutComponentsId, OwnPageChangeDate),
                // Dated before every change window the tests use, so these two only take part in the
                // GetByIdsAsync cases that name them explicitly.
                Page(PageWithMalformedMarkerId, OldDate, RawPageWithMalformedMarkerContent),
                Page(PageWithSameComponentId, OldDate, RawPageWithComponentContent));
            context.AddRange(
                Component(ComponentId, ComponentChangeDate),
                Component(FutureComponentId, FutureComponentChangeDate),
                Component(OlderComponentId, OlderComponentChangeDate),
                Component(LatestComponentId, LatestComponentChangeDate));
            context.AddRange(
                Reference(PageWithComponentId, ComponentId),
                Reference(PageWithFutureComponentId, FutureComponentId),
                Reference(PageWithMultipleComponentsId, OlderComponentId),
                Reference(PageWithMultipleComponentsId, LatestComponentId));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task<string> LoadRawContentAsync(
            string pageId,
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();
            return await context.Set<PageBuilderContentEntity>()
                .Where(x => x.Id == pageId)
                .Select(x => x.PageContent)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }

        private static PageBuilderPageEntity Page(string id, DateTime changeDate, string content = null)
        {
            return new PageBuilderPageEntity
            {
                Id = id,
                GroupId = GroupId,
                StoreId = StoreId,
                Status = "Published",
                CreatedDate = changeDate,
                ModifiedDate = changeDate,
                Content = content == null
                    ? null
                    : new PageBuilderContentEntity
                    {
                        Id = id,
                        PageContent = content,
                    },
            };
        }

        private static PageBuilderSharedComponentEntity Component(string id, DateTime changeDate)
        {
            return new PageBuilderSharedComponentEntity
            {
                Id = id,
                StoreId = StoreId,
                Name = id,
                CreatedDate = OldDate,
                ModifiedDate = changeDate,
                Content = new PageBuilderSharedComponentContentEntity
                {
                    Id = id,
                    ComponentContent = id == ComponentId
                        ? SharedComponentContent
                        : "{ \"settings\": {}, \"content\": [] }",
                },
            };
        }

        private static PageBuilderSharedComponentReferenceEntity Reference(string pageId, string componentId)
        {
            return new PageBuilderSharedComponentReferenceEntity
            {
                Id = $"{pageId}-{componentId}",
                PageId = pageId,
                SharedComponentId = componentId,
            };
        }
    }

    private const string StoreId = "store";
    private const string GroupId = "group";
    private const string PageWithComponentId = "page-component";
    private const string PageWithFutureComponentId = "page-future-component";
    private const string PageWithMultipleComponentsId = "page-multiple-components";
    private const string PageWithMissingComponentId = "page-missing-component";
    private const string PageWithoutComponentsId = "page-without-components";
    private const string PageWithMalformedMarkerId = "page-malformed-marker";
    private const string PageWithSameComponentId = "page-same-component";
    private const string ComponentId = "component";
    private const string FutureComponentId = "component-future";
    private const string OlderComponentId = "component-older";
    private const string LatestComponentId = "component-latest";
    private const string RawPageWithComponentContent = "{ \"settings\": {}, \"content\": [{ \"id\": \"placement\", \"type\": \"componentRef\", \"componentRef\": \"component\" }] }";
    private const string RawPageWithMissingComponentContent = "{ \"settings\": {}, \"content\": [{ \"id\": \"ordinary\", \"type\": \"hero\" }, { \"id\": \"missing\", \"type\": \"componentRef\", \"componentRef\": \"missing-component\" }] }";
    // A marker carrying an extra field is not a valid componentRef and not an ordinary section either.
    private const string RawPageWithMalformedMarkerContent = "{ \"settings\": {}, \"content\": [{ \"id\": \"placement\", \"type\": \"componentRef\", \"componentRef\": \"component\", \"label\": \"extra\" }] }";
    private const string SharedComponentContent = "{ \"settings\": {}, \"content\": [{ \"id\": \"shared\", \"type\": \"shared-hero\", \"title\": \"Shared title\" }] }";
    private const string UpdatedSharedComponentContent = "{ \"settings\": {}, \"content\": [{ \"id\": \"shared\", \"type\": \"shared-hero\", \"title\": \"Updated shared title\" }] }";
    private static readonly DateTime OldDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OwnPageChangeDate = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowStart = new(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ComponentChangeDate = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OlderComponentChangeDate = new(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LatestComponentChangeDate = new(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FutureComponentChangeDate = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
}
