using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Domain;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetReferenceMigrationServiceTests
{
    [Fact]
    public async Task RebuildLinkedComponentIndexAsync_BackfillsUnusedComponentContent()
    {
        var index = new RecordingLinkedComponentAssetReferenceIndexService();

        await PageBuilderAssetReferenceMigrationService.RebuildLinkedComponentIndexAsync(
            "unused-component",
            "component-assets",
            index,
            TestContext.Current.CancellationToken);

        Assert.Equal("unused-component", index.LinkedComponentId);
        Assert.Equal("component-assets", index.Content);
    }

    [Fact]
    public async Task RebuildPageAssetReferenceIndex_LegacyRepositoryUsesLegacyIndexServices()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var loadedPageIds = new List<string>();
        var indexedPageIds = new List<string>();
        var migration = new PageBuilderAssetReferenceMigrationService(
            () => new LegacyRepository(new PageBuilderModuleRepository(database.CreateContext())),
            linkedComponentAssetReferenceIndexService: null,
            settingsManager: null,
            new RecordingGroupedPageService(loadedPageIds),
            new RecordingPageAssetReferenceIndexService(indexedPageIds));

        await migration.RebuildPageAssetReferenceIndex();

        Assert.Equal(101, loadedPageIds.Count);
        Assert.Equal(loadedPageIds, indexedPageIds);
    }

    [Fact]
    public async Task RebuildLinkedComponentAssetReferenceIndex_LegacyRepositorySkipsUnsupportedBackfill()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var index = new RecordingLinkedComponentAssetReferenceIndexService();
        var migration = new PageBuilderAssetReferenceMigrationService(
            () => new LegacyRepository(new PageBuilderModuleRepository(database.CreateContext())),
            index,
            settingsManager: null);

        await migration.RebuildLinkedComponentAssetReferenceIndex();

        Assert.Null(index.LinkedComponentId);
    }

    [Fact]
    public async Task RebuildPageAssetReferenceIndex_ConcurrentDeletionDoesNotSkipNextPage()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var processedCount = 0;
        Func<IPageBuilderModuleRepository> repositoryFactory = () =>
            new CoordinatedRepository(
                database.CreateContext(),
                async () =>
                {
                    processedCount++;
                    if (processedCount == 50)
                    {
                        await database.DeletePageAsync("page-010");
                    }
                });
        var migration = new PageBuilderAssetReferenceMigrationService(
            repositoryFactory,
            linkedComponentAssetReferenceIndexService: null,
            settingsManager: null);

        await migration.RebuildPageAssetReferenceIndex();

        await using var verificationContext = database.CreateContext();
        var remainingPageIds = await verificationContext.Set<PageBuilderPageEntity>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var indexedReferences = await verificationContext.Set<PageBuilderAssetReferenceEntity>()
            .GroupBy(x => x.PageId)
            .Select(x => new
            {
                PageId = x.Key,
                Urls = x.Select(reference => reference.NormalizedAssetUrl).ToArray(),
            })
            .ToDictionaryAsync(x => x.PageId, x => x.Urls, TestContext.Current.CancellationToken);

        Assert.Equal(100, remainingPageIds.Length);
        Assert.Equal(101, processedCount);
        foreach (var pageId in remainingPageIds)
        {
            Assert.Equal([MigrationDatabase.CurrentAssetUrl], indexedReferences[pageId]);
        }
    }

    [Fact]
    public async Task RebuildLinkedComponentAssetReferenceIndex_ConcurrentDeletionDoesNotSkipNextComponent()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var index = new DeletingLinkedComponentIndex(database);
        var migration = new PageBuilderAssetReferenceMigrationService(
            () => new PageBuilderModuleRepository(database.CreateContext()),
            index,
            settingsManager: null);

        await migration.RebuildLinkedComponentAssetReferenceIndex();

        await using var context = database.CreateContext();
        var remainingIds = await context.Set<PageBuilderLinkedComponentEntity>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(100, remainingIds.Length);
        Assert.Equal(101, index.ComponentIds.Count);
        Assert.All(remainingIds, id => Assert.Contains(id, index.ComponentIds));
    }

    [Fact]
    public async Task RebuildPageAssetReferenceIndex_ConcurrentPageSaveCannotLeaveStaleSnapshotLast()
    {
        await using var database = await MigrationDatabase.CreateAsync();
        var migrationHasPageLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMigration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseOnce = 0;
        Func<IPageBuilderModuleRepository> repositoryFactory = () =>
            new PausingRepository(
                database.CreateContext(),
                async pageIds =>
                {
                    if (pageIds.Contains("page-000") && Interlocked.CompareExchange(ref pauseOnce, 1, 0) == 0)
                    {
                        migrationHasPageLock.SetResult();
                        await releaseMigration.Task.WaitAsync(TestContext.Current.CancellationToken);
                    }
                });
        var migration = new PageBuilderAssetReferenceMigrationService(
            repositoryFactory,
            linkedComponentAssetReferenceIndexService: null,
            settingsManager: null);

        var migrationTask = migration.RebuildPageAssetReferenceIndex();
        await migrationHasPageLock.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pageSave = Task.Run(
            async () =>
            {
                saveStarted.SetResult();
                await database.SavePageAsync("page-000", MigrationDatabase.ReplacementAssetUrl);
            },
            TestContext.Current.CancellationToken);
        await saveStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        Assert.False(pageSave.IsCompleted);

        releaseMigration.SetResult();
        await Task.WhenAll(migrationTask, pageSave).WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        await using var context = database.CreateContext();
        var urls = await context.Set<PageBuilderAssetReferenceEntity>()
            .Where(x => x.PageId == "page-000")
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([MigrationDatabase.ReplacementAssetUrl], urls);
    }

    [Fact]
    public void KeysetPredicates_TranslateForSqlServerProvider()
    {
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseSqlServer("Server=localhost;Database=translation-only;TrustServerCertificate=True")
            .Options;

        AssertKeysetPredicatesTranslate(options);
    }

    [Fact]
    public void KeysetPredicates_TranslateForPostgreSqlProvider()
    {
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseNpgsql("Host=localhost;Database=translation-only;Username=test;Password=test")
            .Options;

        AssertKeysetPredicatesTranslate(options);
    }

    [Fact]
    public void KeysetPredicates_TranslateForMySqlProvider()
    {
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseMySql(
                "Server=localhost;Database=translation-only;User=test;Password=test",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        AssertKeysetPredicatesTranslate(options);
    }

    private static void AssertKeysetPredicatesTranslate(
        DbContextOptions<PageBuilderModuleDbContext> options)
    {
        using var context = new PageBuilderModuleDbContext(options);

        var pageSql = PageBuilderAssetReferenceMigrationService.ApplyPageCursor(
                context.Set<PageBuilderPageEntity>(),
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                "page-050")
            .ToQueryString();
        var componentSql = PageBuilderAssetReferenceMigrationService.ApplyLinkedComponentCursor(
                context.Set<PageBuilderLinkedComponentContentEntity>(),
                "component-050")
            .ToQueryString();

        Assert.Contains("WHERE", pageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", componentSql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingLinkedComponentAssetReferenceIndexService
        : IPageBuilderLinkedComponentAssetReferenceIndexService
    {
        public string LinkedComponentId { get; private set; }

        public string Content { get; private set; }

        public Task RebuildIndexAsync(
            string linkedComponentId,
            string content,
            CancellationToken cancellationToken = default)
        {
            LinkedComponentId = linkedComponentId;
            Content = content;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGroupedPageService(IList<string> pageIds) : IGroupedPageService
    {
        public Task<IList<GroupedPageBuilderPage>> GetAsync(
            IList<string> ids,
            string responseGroup = null,
            bool clone = true) => Task.FromResult<IList<GroupedPageBuilderPage>>([]);

        public Task SaveChangesAsync(IList<GroupedPageBuilderPage> models) => Task.CompletedTask;
        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => Task.CompletedTask;

        public Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default)
        {
            pageIds.Add(pageId);
            return Task.FromResult("{ \"settings\": {}, \"content\": [] }");
        }

        public Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> LoadContentToStreamAsync(
            string pageId,
            Stream stream,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task SaveStreamAsContentAsync(
            string pageId,
            Stream stream,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CopyPageContentAsync(
            string sourcePageId,
            string targetPageId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TryDeleteEmptyDraftAsync(
            string pageId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class RecordingPageAssetReferenceIndexService(IList<string> pageIds)
        : IPageBuilderAssetReferenceIndexService
    {
        public Task RebuildPageIndexAsync(
            string pageId,
            string content,
            CancellationToken cancellationToken = default)
        {
            pageIds.Add(pageId);
            return Task.CompletedTask;
        }

        public Task DeletePageIndexAsync(
            IEnumerable<string> pageIds,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteGroupIndexAsync(
            IEnumerable<string> groupIds,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class LegacyRepository(PageBuilderModuleRepository inner) : IPageBuilderModuleRepository
    {
        public IQueryable<PageBuilderPageEntity> PageBuilderPages => inner.PageBuilderPages;
        public IQueryable<GroupedPageBuilderPageEntity> GroupedPageBuilderPages => inner.GroupedPageBuilderPages;
        public IQueryable<PageBuilderAssetReferenceEntity> PageBuilderAssetReferences => inner.PageBuilderAssetReferences;
        public IUnitOfWork UnitOfWork => inner.UnitOfWork;

        public Task<IList<PageBuilderPageEntity>> GetPageBuilderPagesByIdsAsync(
            IList<string> ids,
            string responseGroup) => inner.GetPageBuilderPagesByIdsAsync(ids, responseGroup);

        public Task<IList<GroupedPageBuilderPageEntity>> GetGroupedPageBuilderPagesByIdsAsync(
            IList<string> ids,
            string responseGroup) => inner.GetGroupedPageBuilderPagesByIdsAsync(ids, responseGroup);

        public void Attach<T>(T item) where T : class => inner.Attach(item);
        public void Add<T>(T item) where T : class => inner.Add(item);
        public void Update<T>(T item) where T : class => inner.Update(item);
        public void Remove<T>(T item) where T : class => inner.Remove(item);
        public void Dispose() => inner.Dispose();
    }

    private sealed class CoordinatedRepository(
        PageBuilderModuleDbContext dbContext,
        Func<Task> afterPage) : PageBuilderModuleRepository(dbContext)
    {
        public override async Task ExecuteUnderPageWriteLocksAsync(
            IEnumerable<string> pageIds,
            Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            await base.ExecuteUnderPageWriteLocksAsync(pageIds, operation, cancellationToken);
            await afterPage();
        }
    }

    private sealed class PausingRepository(
        PageBuilderModuleDbContext dbContext,
        Func<IEnumerable<string>, Task> afterPageLocks) : PageBuilderModuleRepository(dbContext)
    {
        public override Task ExecuteUnderPageWriteLocksAsync(
            IEnumerable<string> pageIds,
            Func<PageBuilderModuleDbContext, CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            var ids = pageIds.ToArray();
            return base.ExecuteUnderPageWriteLocksAsync(
                ids,
                async (context, transactionCancellationToken) =>
                {
                    await afterPageLocks(ids);
                    await operation(context, transactionCancellationToken);
                },
                cancellationToken);
        }
    }

    private sealed class DeletingLinkedComponentIndex(MigrationDatabase database)
        : IPageBuilderLinkedComponentAssetReferenceIndexService
    {
        public IList<string> ComponentIds { get; } = [];

        public async Task RebuildIndexAsync(
            string linkedComponentId,
            string content,
            CancellationToken cancellationToken = default)
        {
            ComponentIds.Add(linkedComponentId);
            if (ComponentIds.Count == 50)
            {
                await database.DeleteComponentAsync("component-010");
            }
        }
    }

    private sealed class MigrationDatabase : IAsyncDisposable
    {
        public const string CurrentAssetUrl = "/stores/store/current.png";
        public const string ReplacementAssetUrl = "/stores/store/replacement.png";
        private const string StaleAssetUrl = "/stores/store/stale.png";
        private readonly SqliteConnection _anchorConnection;

        private MigrationDatabase(string connectionString, SqliteConnection anchorConnection)
        {
            ConnectionString = connectionString;
            _anchorConnection = anchorConnection;
        }

        private string ConnectionString { get; }

        public static async Task<MigrationDatabase> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"page-builder-migration-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
            }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new MigrationDatabase(connectionString, anchorConnection);

            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var createdDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            context.Add(new GroupedPageBuilderPageEntity
            {
                Id = "group",
                StoreId = "store",
                CreatedDate = createdDate,
            });

            for (var index = 0; index < 101; index++)
            {
                var pageId = $"page-{index:D3}";
                context.Add(new PageBuilderPageEntity
                {
                    Id = pageId,
                    GroupId = "group",
                    StoreId = "store",
                    Status = "Published",
                    CreatedDate = createdDate,
                    Content = new PageBuilderContentEntity
                    {
                        Id = pageId,
                        PageContent = $"{{ \"settings\": {{ \"image\": \"{CurrentAssetUrl}\" }}, \"content\": [] }}",
                    },
                });
                context.Add(new PageBuilderAssetReferenceEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    PageId = pageId,
                    NormalizedAssetUrl = StaleAssetUrl,
                    NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(StaleAssetUrl),
                });

                var componentId = $"component-{index:D3}";
                context.Add(new PageBuilderLinkedComponentEntity
                {
                    Id = componentId,
                    StoreId = "store",
                    Name = componentId,
                    CreatedDate = createdDate,
                    Content = new PageBuilderLinkedComponentContentEntity
                    {
                        Id = componentId,
                        ComponentContent = "{ \"settings\": {}, \"content\": [] }",
                    },
                });
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            return database;
        }

        public PageBuilderModuleDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
                .UseSqlite(ConnectionString)
                .Options;
            return new PageBuilderModuleDbContext(options);
        }

        public async Task DeletePageAsync(string pageId)
        {
            await using var context = CreateContext();
            var page = await context.Set<PageBuilderPageEntity>()
                .SingleAsync(x => x.Id == pageId, TestContext.Current.CancellationToken);
            context.Remove(page);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task DeleteComponentAsync(string componentId)
        {
            await using var context = CreateContext();
            await context.Set<PageBuilderLinkedComponentEntity>()
                .Where(x => x.Id == componentId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        public async Task SavePageAsync(string pageId, string assetUrl)
        {
            await using var context = CreateContext();
            var repository = new SqliteContentStreamRepository(context);
            var content = $"{{ \"settings\": {{ \"image\": \"{assetUrl}\" }}, \"content\": [] }}";
            using var reader = new StringReader(content);
            await repository.SaveBinaryAsync(
                pageId,
                reader,
                (dbContext, cancellationToken) =>
                    PageBuilderPageIndexing.RebuildAfterRawContentWriteAsync(
                        dbContext,
                        pageId,
                        content,
                        "store",
                        cancellationToken),
                TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }
    }

    private sealed class SqliteContentStreamRepository(PageBuilderModuleDbContext dbContext)
        : ContentStreamRepository(dbContext)
    {
        protected override string QuoteOpen => "\"";

        protected override string QuoteClose => "\"";

        protected override string AppendContentChunkSql =>
            $"UPDATE {Table} SET {ContentColumn} = {ContentColumn} || @chunk WHERE {IdColumn} = @id";

        protected override void SetIdParameter(DbCommand cmd, string value)
        {
            cmd.Parameters.Add(new SqliteParameter("@id", value));
        }

        protected override void SetContentChunk(DbCommand cmd, string chunk)
        {
            cmd.Parameters.Add(new SqliteParameter("@chunk", chunk));
        }
    }
}
