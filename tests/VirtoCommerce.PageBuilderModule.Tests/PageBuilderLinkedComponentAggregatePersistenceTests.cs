using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentAggregatePersistenceTests
{
    [Fact]
    public async Task SaveWithContentAsync_PersistsAggregateAndPublishesEventsAfterCommit()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);
        var model = new PageBuilderLinkedComponent
        {
            StoreId = StoreId,
            Name = " Shared hero ",
        };

        await service.SaveWithContentAsync(
            model,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(model.Id));
        await using var context = database.CreateContext();
        var component = await context.Set<PageBuilderLinkedComponentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReference = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(model.Id, component.Id);
        Assert.Equal("Shared hero", component.Name);
        Assert.Equal(model.Id, content.Id);
        Assert.Equal(ContentWithAsset(NewAssetUrl), content.ComponentContent);
        Assert.Equal(NewAssetUrl, assetReference.NormalizedAssetUrl);
        Assert.Equal(
            [
                typeof(PageBuilderLinkedComponentChangingEvent),
                typeof(PageBuilderLinkedComponentChangedEvent),
                typeof(PageBuilderLinkedComponentContentChangedEvent),
            ],
            events.EventTypes);
    }

    [Fact]
    public async Task SaveWithContentAsync_CommitFailureRollsBackMetadataContentAndAssetReferences()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.FailComponentAssetReferenceInsertsAsync();
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);
        var model = new PageBuilderLinkedComponent
        {
            StoreId = StoreId,
            Name = "Shared hero",
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => service.SaveWithContentAsync(
            model,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken));

        await using var context = database.CreateContext();
        Assert.Empty(await context.Set<PageBuilderLinkedComponentEntity>()
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.Set<PageBuilderLinkedComponentContentEntity>()
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal([typeof(PageBuilderLinkedComponentChangingEvent)], events.EventTypes);
    }

    [Fact]
    public async Task SaveContentAsync_CommitFailureKeepsPreviousContentAndExactAssetIndex()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        await database.FailComponentAssetReferenceInsertsAsync();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentContentService(database.RepositoryFactory, events);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.SaveContentAsync(
            ComponentId,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken));

        await using var context = database.CreateContext();
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(OldAssetUrl), content.ComponentContent);
        Assert.Equal([OldAssetUrl], assetReferences);
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task SaveContentAsync_ReplacesComponentAssetIndexExactly()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentContentService(database.RepositoryFactory, events);

        await service.SaveContentAsync(
            ComponentId,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);

        await using var context = database.CreateContext();
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal([NewAssetUrl], assetReferences);
        Assert.Equal([typeof(PageBuilderLinkedComponentContentChangedEvent)], events.EventTypes);
    }

    [Fact]
    public async Task SaveContentAsync_WhenAssetIsStillUsed_KeepsSingleExactReference()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentContentService(database.RepositoryFactory, events);

        await service.SaveContentAsync(
            ComponentId,
            $"{{ \"settings\": {{ \"image\": \"{OldAssetUrl}\", \"alt\": \"Updated\" }}, \"content\": [] }}",
            TestContext.Current.CancellationToken);

        await using var context = database.CreateContext();
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal([OldAssetUrl], assetReferences);
        Assert.Equal([typeof(PageBuilderLinkedComponentContentChangedEvent)], events.EventTypes);
    }

    [Fact]
    public async Task SaveContentAsync_ConcurrentWritersKeepContentAndAssetIndexOnSameVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentWithoutAssetsAsync();
        var firstWriterHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriterHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstService = new PageBuilderLinkedComponentContentService(
            () => new CoordinatedPageBuilderModuleRepository(
                database.CreateContext(),
                async cancellationToken =>
                {
                    firstWriterHasLock.SetResult();
                    await releaseFirstWriter.Task.WaitAsync(cancellationToken);
                }),
            new RecordingEventPublisher());
        var secondService = new PageBuilderLinkedComponentContentService(
            () => new CoordinatedPageBuilderModuleRepository(
                database.CreateContext(),
                _ =>
                {
                    secondWriterHasLock.SetResult();
                    return Task.CompletedTask;
                }),
            new RecordingEventPublisher());

        var firstWrite = firstService.SaveContentAsync(
            ComponentId,
            ContentWithAsset(OldAssetUrl),
            TestContext.Current.CancellationToken);
        await firstWriterHasLock.Task.WaitAsync(TestContext.Current.CancellationToken);

        var secondWrite = Task.Run(
            async () =>
            {
                secondWriterStarted.SetResult();
                await secondService.SaveContentAsync(
                    ComponentId,
                    ContentWithAsset(NewAssetUrl),
                    TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken);
        await secondWriterStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(secondWriterHasLock.Task.IsCompleted);

        releaseFirstWriter.SetResult();
        await Task.WhenAll(firstWrite, secondWrite);
        Assert.True(secondWriterHasLock.Task.IsCompleted);

        await using var context = database.CreateContext();
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .Where(x => x.Id == ComponentId)
            .Select(x => x.ComponentContent)
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Where(x => x.LinkedComponentId == ComponentId)
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(NewAssetUrl), content);
        Assert.Equal([NewAssetUrl], assetReferences);
    }

    [Fact]
    public async Task SaveWithContentAsync_AndSaveContentAsync_SerializeThroughSameComponentLock()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentWithoutAssetsAsync();
        var aggregateWriterHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAggregateWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contentWriterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contentWriterHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cache = new TestPlatformMemoryCache();
        var aggregateService = new PageBuilderLinkedComponentService(
            () => new CoordinatedPageBuilderModuleRepository(
                database.CreateContext(),
                async cancellationToken =>
                {
                    aggregateWriterHasLock.SetResult();
                    await releaseAggregateWriter.Task.WaitAsync(cancellationToken);
                }),
            cache,
            new RecordingEventPublisher());
        var contentService = new PageBuilderLinkedComponentContentService(
            () => new CoordinatedPageBuilderModuleRepository(
                database.CreateContext(),
                _ =>
                {
                    contentWriterHasLock.SetResult();
                    return Task.CompletedTask;
                }),
            new RecordingEventPublisher());

        var aggregateWrite = aggregateService.SaveWithContentAsync(
            new PageBuilderLinkedComponent
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Shared hero",
            },
            ContentWithAsset(OldAssetUrl),
            TestContext.Current.CancellationToken);
        await aggregateWriterHasLock.Task.WaitAsync(TestContext.Current.CancellationToken);

        var contentWrite = Task.Run(
            async () =>
            {
                contentWriterStarted.SetResult();
                await contentService.SaveContentAsync(
                    ComponentId,
                    ContentWithAsset(NewAssetUrl),
                    TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken);
        await contentWriterStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(contentWriterHasLock.Task.IsCompleted);

        releaseAggregateWriter.SetResult();
        await Task.WhenAll(aggregateWrite, contentWrite);
        Assert.True(contentWriterHasLock.Task.IsCompleted);

        await using var context = database.CreateContext();
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .Where(x => x.Id == ComponentId)
            .Select(x => x.ComponentContent)
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Where(x => x.LinkedComponentId == ComponentId)
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(NewAssetUrl), content);
        Assert.Equal([NewAssetUrl], assetReferences);
    }

    [Fact]
    public async Task RebuildIndexAsync_WhenPassedContentIsStale_UsesCurrentPersistedContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(NewAssetUrl);
        var service = new PageBuilderLinkedComponentAssetReferenceIndexService(database.RepositoryFactory);

        await service.RebuildIndexAsync(
            ComponentId,
            ContentWithAsset(OldAssetUrl),
            TestContext.Current.CancellationToken);

        await using var context = database.CreateContext();
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Where(x => x.LinkedComponentId == ComponentId)
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal([NewAssetUrl], assetReferences);
    }

    private static string ContentWithAsset(string assetUrl)
    {
        return $"{{ \"settings\": {{ \"image\": \"{assetUrl}\" }}, \"content\": [] }}";
    }

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public IList<Type> EventTypes { get; } = [];

        public Task Publish<T>(T @event, CancellationToken cancellationToken = default)
            where T : IEvent
        {
            EventTypes.Add(@event.GetType());
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

    private sealed class CoordinatedPageBuilderModuleRepository(
        PageBuilderModuleDbContext dbContext,
        Func<CancellationToken, Task> afterLock)
        : PageBuilderModuleRepository(dbContext)
    {
        public override Task<bool> ExecuteUnderLinkedComponentWriteLockAsync(
            string linkedComponentId,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            return base.ExecuteUnderLinkedComponentWriteLockAsync(
                linkedComponentId,
                async transactionCancellationToken =>
                {
                    await afterLock(transactionCancellationToken);
                    await operation(transactionCancellationToken);
                },
                cancellationToken);
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _anchorConnection;

        private TestDatabase(string connectionString, SqliteConnection anchorConnection)
        {
            ConnectionString = connectionString;
            _anchorConnection = anchorConnection;
        }

        public string ConnectionString { get; }

        public Func<IPageBuilderModuleRepository> RepositoryFactory =>
            () => new PageBuilderModuleRepository(CreateContext());

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"page-builder-component-aggregate-{Guid.NewGuid():N}",
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

        public async Task SeedComponentAsync(string assetUrl)
        {
            await using var context = CreateContext();
            context.Add(new PageBuilderLinkedComponentEntity
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Shared hero",
                CreatedDate = DateTime.UtcNow,
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = ComponentId,
                    ComponentContent = ContentWithAsset(assetUrl),
                },
            });
            context.Add(new PageBuilderLinkedComponentAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                LinkedComponentId = ComponentId,
                NormalizedAssetUrl = assetUrl,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(assetUrl),
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task SeedComponentWithoutAssetsAsync()
        {
            await using var context = CreateContext();
            context.Add(new PageBuilderLinkedComponentEntity
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Shared hero",
                CreatedDate = DateTime.UtcNow,
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = ComponentId,
                    ComponentContent = "{ \"settings\": {}, \"content\": [] }",
                },
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task FailComponentAssetReferenceInsertsAsync()
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER FailComponentAssetReferenceInsert
                BEFORE INSERT ON PageBuilderLinkedComponentAssetReference
                BEGIN
                    SELECT RAISE(ABORT, 'simulated component asset index failure');
                END;
                """,
                TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }
    }

    private const string StoreId = "store";
    private const string ComponentId = "component";
    private const string OldAssetUrl = "/stores/store/old.png";
    private const string NewAssetUrl = "/stores/store/new.png";
}
