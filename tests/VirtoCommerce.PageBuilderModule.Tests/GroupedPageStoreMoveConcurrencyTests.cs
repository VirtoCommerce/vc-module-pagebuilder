using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Pages.Core.Events;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GroupedPageStoreMoveConcurrencyTests
{
    private const string ComponentId = "component";
    private const string GroupId = "group";
    private const string PageId = "page";

    [Fact]
    public async Task SaveChangesAsync_ReferenceAppearingInsideWriteLockRejectsMoveAndRollsBack()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var service = new GroupedPageService(
            () => new CoordinatedRepository(database.CreateContext()),
            () => throw new InvalidOperationException("Content repository is not used by this test."),
            cache,
            new NoopEventPublisher(),
            NullLogger<GroupedPageService>.Instance,
            new NoopAssetReferenceIndexService(),
            new NoopLinkedComponentReferenceIndexService());
        var model = new GroupedPageBuilderPage
        {
            Id = GroupId,
            StoreId = "store-b",
            Pages =
            [
                new PageBuilderPage
                {
                    Id = PageId,
                    GroupId = GroupId,
                    StoreId = "store-a",
                    Status = "Draft",
                },
            ],
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => service.SaveChangesAsync([model]));

        await using var verificationContext = database.CreateContext();
        Assert.Equal(
            "store-a",
            await verificationContext.Set<GroupedPageBuilderPageEntity>()
                .Where(x => x.Id == GroupId)
                .Select(x => x.StoreId)
                .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "store-a",
            await verificationContext.Set<PageBuilderPageEntity>()
                .Where(x => x.Id == PageId)
                .Select(x => x.StoreId)
                .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verificationContext.Set<PageBuilderLinkedComponentReferenceEntity>()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChangesAsync_LegacyGroupMoveCommitsGroupAndPageStoreTogether()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var service = new GroupedPageService(
            () => new PageBuilderModuleRepository(database.CreateContext()),
            () => throw new InvalidOperationException("Content repository is not used by this test."),
            cache,
            new NoopEventPublisher(),
            NullLogger<GroupedPageService>.Instance,
            new NoopAssetReferenceIndexService(),
            new NoopLinkedComponentReferenceIndexService());
        var model = new GroupedPageBuilderPage
        {
            Id = GroupId,
            StoreId = "store-b",
            Pages =
            [
                new PageBuilderPage
                {
                    Id = PageId,
                    GroupId = GroupId,
                    StoreId = "store-a",
                    Status = "Draft",
                },
            ],
        };

        await service.SaveChangesAsync([model]);

        await using var verificationContext = database.CreateContext();
        Assert.Equal(
            "store-b",
            await verificationContext.Set<GroupedPageBuilderPageEntity>()
                .Where(x => x.Id == GroupId)
                .Select(x => x.StoreId)
                .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "store-b",
            await verificationContext.Set<PageBuilderPageEntity>()
                .Where(x => x.Id == PageId)
                .Select(x => x.StoreId)
                .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryDeleteEmptyDraftAsync_DeletesOnlyStillEmptyDraft()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = CreateService(database, cache, events);
        var groupedSearchCacheKey = $"grouped-search-{Guid.NewGuid():N}";
        using (var cacheEntry = cache.CreateEntry(groupedSearchCacheKey))
        {
            cacheEntry.Value = true;
            cacheEntry.ExpirationTokens.Add(
                GenericSearchCachingRegion<GroupedPageBuilderPage>.CreateChangeToken());
        }
        Assert.True(cache.TryGetValue(groupedSearchCacheKey, out _));

        var deleted = await service.TryDeleteEmptyDraftAsync(
            PageId,
            TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.False(cache.TryGetValue(groupedSearchCacheKey, out _));
        var pagesEvent = Assert.Single(events.Events.OfType<PagesDomainEvent>());
        Assert.Equal(PageId, pagesEvent.Id);
        Assert.Equal(PageId, pagesEvent.Page.Id);
        Assert.Equal(PageOperation.Delete, pagesEvent.Operation);
        await using var verificationContext = database.CreateContext();
        Assert.False(await verificationContext.Set<PageBuilderPageEntity>()
            .AnyAsync(x => x.Id == PageId, TestContext.Current.CancellationToken));
        Assert.True(await verificationContext.Set<GroupedPageBuilderPageEntity>()
            .AnyAsync(x => x.Id == GroupId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryDeleteEmptyDraftAsync_PreservesDraftWithCommittedContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedAsync();
        await database.SetPageContentAsync("{ \"concurrent\": true }");
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = CreateService(database, cache, events);

        var deleted = await service.TryDeleteEmptyDraftAsync(
            PageId,
            TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Empty(events.Events);
        await using var verificationContext = database.CreateContext();
        Assert.True(await verificationContext.Set<PageBuilderPageEntity>()
            .AnyAsync(x => x.Id == PageId, TestContext.Current.CancellationToken));
    }

    private static GroupedPageService CreateService(
        TestDatabase database,
        IPlatformMemoryCache cache,
        IEventPublisher eventPublisher = null)
    {
        return new GroupedPageService(
            () => new PageBuilderModuleRepository(database.CreateContext()),
            () => throw new InvalidOperationException("Content repository is not used by this test."),
            cache,
            eventPublisher ?? new NoopEventPublisher(),
            NullLogger<GroupedPageService>.Instance,
            new NoopAssetReferenceIndexService(),
            new NoopLinkedComponentReferenceIndexService());
    }

    private sealed class CoordinatedRepository(PageBuilderModuleDbContext dbContext)
        : PageBuilderModuleRepository(dbContext)
    {
        public override Task ExecuteUnderGroupedPageWriteLocksAsync(
            IEnumerable<string> groupIds,
            Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            return base.ExecuteUnderGroupedPageWriteLocksAsync(
                groupIds,
                async (context, transactionCancellationToken) =>
                {
                    context.Add(new PageBuilderLinkedComponentReferenceEntity
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        PageId = PageId,
                        LinkedComponentId = ComponentId,
                    });
                    await context.SaveChangesAsync(transactionCancellationToken);
                    await operation(context, transactionCancellationToken);
                },
                cancellationToken);
        }
    }

    private sealed class NoopAssetReferenceIndexService : IPageBuilderAssetReferenceIndexService
    {
        public Task RebuildPageIndexAsync(
            string pageId,
            string content,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeletePageIndexAsync(
            IEnumerable<string> pageIds,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteGroupIndexAsync(
            IEnumerable<string> groupIds,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopEventPublisher : IEventPublisher
    {
        public Task Publish<T>(T @event, CancellationToken cancellationToken = default)
            where T : IEvent => Task.CompletedTask;
    }

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public IList<IEvent> Events { get; } = [];

        public Task Publish<T>(T @event, CancellationToken cancellationToken = default)
            where T : IEvent
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
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

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _anchorConnection;

        private TestDatabase(string connectionString, SqliteConnection anchorConnection)
        {
            ConnectionString = connectionString;
            _anchorConnection = anchorConnection;
        }

        private string ConnectionString { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"page-builder-store-move-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 10,
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
            var now = DateTime.UtcNow;
            context.Add(new GroupedPageBuilderPageEntity
            {
                Id = GroupId,
                StoreId = "store-a",
                CreatedDate = now,
                Pages = new ObservableCollection<PageBuilderPageEntity>
                {
                    new()
                    {
                        Id = PageId,
                        GroupId = GroupId,
                        StoreId = "store-a",
                        Status = "Draft",
                        CreatedDate = now,
                    },
                },
            });
            context.Add(new PageBuilderLinkedComponentEntity
            {
                Id = ComponentId,
                StoreId = "store-a",
                Name = "Shared component",
                CreatedDate = now,
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = ComponentId,
                    ComponentContent = "{}",
                },
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task SetPageContentAsync(string content)
        {
            await using var context = CreateContext();
            var pageContent = new PageBuilderContentEntity
            {
                Id = PageId,
                PageContent = content,
            };
            context.Attach(pageContent);
            context.Entry(pageContent).Property(x => x.PageContent).IsModified = true;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }
    }
}
