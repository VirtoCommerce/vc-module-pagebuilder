using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public partial class PageContentAtomicWriteTests
{
    [Fact]
    public async Task ConditionalSave_FirstDraftInvalidatesPreviouslyEmptyPageSearch()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = CreateContext(database.ConnectionString))
        {
            context.RemoveRange(await context.Set<PageBuilderPageEntity>().Where(x => x.GroupId == GroupId)
                .ToArrayAsync(TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var group = await LoadGroupAsync(database.ConnectionString);
        using var cache = new TestPlatformMemoryCache();
        var service = new GroupedPageService(
            () => new PageBuilderModuleRepository(CreateContext(database.ConnectionString)),
            () => new SqliteContentStreamRepository(CreateContext(database.ConnectionString)),
            cache, new NoopEventPublisher(), NullLogger<GroupedPageService>.Instance);
        var searchKey = $"empty-page-search-{Guid.NewGuid():N}";
        using (var entry = cache.CreateEntry(searchKey))
        {
            entry.Value = Array.Empty<string>();
            entry.ExpirationTokens.Add(GenericSearchCachingRegion<PageBuilderPage>.CreateChangeToken());
        }
        Assert.True(cache.TryGetValue(searchKey, out _));

        var result = await service.SaveGroupContentAsync(group, ComponentAContent,
            PageBuilderContentVersion.Create(group, null), TestContext.Current.CancellationToken);

        await using var verification = CreateContext(database.ConnectionString);
        Assert.Equal(result.PageId, await verification.Set<PageBuilderPageEntity>()
            .Where(x => x.GroupId == GroupId).Select(x => x.Id).SingleAsync(TestContext.Current.CancellationToken));
        Assert.False(cache.TryGetValue(searchKey, out _));
    }

    [Fact]
    public async Task ConditionalSave_StaleAuthorCannotReplaceContentOrIndexes()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        var group = await LoadGroupAsync(database.ConnectionString);
        var version = PageBuilderContentVersion.Create(group, ComponentAContent);
        var result = await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent, version);

        await Assert.ThrowsAsync<PageBuilderContentConflictException>(() =>
            SaveConditionalAsync(database.ConnectionString, group, UpdatedPageContent, version));
        Assert.Equal(ComponentBContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal([ComponentBId], await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal([AssetBUrl], await LoadAssetUrlsAsync(database.ConnectionString));

        await SaveConditionalAsync(database.ConnectionString, group, UpdatedPageContent, result.ETag);
        Assert.Equal(UpdatedPageContent, await LoadContentAsync(database.ConnectionString));
    }

    // SQLite serializes whole transactions: these races prove transaction-level atomicity.
    // Provider row-lock behavior is exercised separately against a live SQL Server.
    [Fact]
    public async Task ConditionalSave_ConcurrentAuthorsWithSameVersionHaveOneWinner()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        var group = await LoadGroupAsync(database.ConnectionString);
        var version = PageBuilderContentVersion.Create(group, ComponentAContent);
        var atCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = SaveConditionalAsync(database.ConnectionString, group, ComponentBContent, version, async ct =>
        {
            atCommit.SetResult();
            await release.Task.WaitAsync(ct);
        });
        await atCommit.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = Task.Run(() => SaveConditionalAsync(database.ConnectionString, group, UpdatedPageContent, version),
            TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(second.IsCompleted);
        release.SetResult();
        await first;
        await Assert.ThrowsAsync<PageBuilderContentConflictException>(() => second);
        Assert.Equal(ComponentBContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal([ComponentBId], await LoadReferenceIdsAsync(database.ConnectionString));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public async Task ConditionalSave_SimultaneousAuthorsPreserveExactlyOneDocumentAndItsIndexes(int authors)
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        var group = await LoadGroupAsync(database.ConnectionString);
        var version = PageBuilderContentVersion.Create(group, ComponentAContent);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = Enumerable.Range(0, authors).Select(index => Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            var content = (index % 2 == 0 ? ComponentBContent : UpdatedPageContent) + new string(' ', index);
            try
            {
                await SaveConditionalAsync(database.ConnectionString, group, content, version);
                return content;
            }
            catch (PageBuilderContentConflictException)
            {
                return null;
            }
        }, TestContext.Current.CancellationToken)).ToArray();
        start.SetResult();

        var winner = Assert.Single(await Task.WhenAll(writes), x => x != null);
        Assert.Equal(winner, await LoadContentAsync(database.ConnectionString));
        Assert.Equal([winner.TrimEnd() == ComponentBContent ? ComponentBId : ComponentAId],
            await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal([AssetBUrl], await LoadAssetUrlsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task ConditionalSave_FirstSaveCreatesOneDraftAndRejectsStalePublishedCopy()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = CreateContext(database.ConnectionString))
        {
            context.Remove(await context.Set<PageBuilderPageEntity>().SingleAsync(x => x.Id == PageId, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentAContent);
        var group = await LoadGroupAsync(database.ConnectionString);
        var version = PageBuilderContentVersion.Create(group, ComponentAContent);
        var result = await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent, version);
        await Assert.ThrowsAsync<PageBuilderContentConflictException>(() =>
            SaveConditionalAsync(database.ConnectionString, group, UpdatedPageContent, version));

        await using var verification = CreateContext(database.ConnectionString);
        var draft = Assert.Single(await verification.Set<PageBuilderPageEntity>()
            .Where(x => x.GroupId == GroupId && x.Status == "Draft").ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(result.PageId, draft.Id);
        Assert.Equal(ComponentBContent, await verification.Set<PageBuilderContentEntity>()
            .Where(x => x.Id == draft.Id).Select(x => x.PageContent).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConditionalSave_UnseededDraftUsesPublishedContentVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentAContent);
        var group = await LoadGroupAsync(database.ConnectionString);
        var result = await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent,
            PageBuilderContentVersion.Create(group, ComponentAContent));
        Assert.Equal(PageId, result.PageId);
        Assert.Equal(ComponentBContent, await LoadContentAsync(database.ConnectionString));
    }

    [Fact]
    public async Task ConditionalSave_IndexFailureRollsBackNewDraftAndLeavesVersionUsable()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = CreateContext(database.ConnectionString))
        {
            context.Remove(await context.Set<PageBuilderPageEntity>().SingleAsync(x => x.Id == PageId, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var group = await LoadGroupAsync(database.ConnectionString);
        var version = PageBuilderContentVersion.Create(group, null);
        await Assert.ThrowsAsync<IOException>(() => SaveConditionalAsync(database.ConnectionString, group,
            ComponentAContent, version, _ => throw new IOException("Index failed")));
        await using var verification = CreateContext(database.ConnectionString);
        Assert.False(await verification.Set<PageBuilderPageEntity>().AnyAsync(x => x.Status == "Draft", TestContext.Current.CancellationToken));
        await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent, version);
    }

    [Fact]
    public async Task ConditionalSave_StoreChangedAfterAuthorizationCannotWrite()
    {
        await using var database = await TestDatabase.CreateAsync();
        var group = await LoadGroupAsync(database.ConnectionString);
        await using (var context = CreateContext(database.ConnectionString))
        {
            await context.Set<GroupedPageBuilderPageEntity>().Where(x => x.Id == GroupId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StoreId, "other-store"), TestContext.Current.CancellationToken);
        }
        await Assert.ThrowsAsync<KeyNotFoundException>(() => SaveConditionalAsync(database.ConnectionString,
            group, ComponentAContent, PageBuilderContentVersion.Create(group, null)));
    }

    [Fact]
    public async Task ConditionalSave_RecreatedGroupCannotUsePreviousAuthorization()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        var group = await LoadGroupAsync(database.ConnectionString);
        await using (var context = CreateContext(database.ConnectionString))
        {
            await context.Set<GroupedPageBuilderPageEntity>().Where(x => x.Id == GroupId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedDate, group.CreatedDate.AddDays(1)), TestContext.Current.CancellationToken);
        }
        await Assert.ThrowsAsync<KeyNotFoundException>(() => SaveConditionalAsync(database.ConnectionString,
            group, ComponentBContent, PageBuilderContentVersion.Create(group, ComponentAContent)));
        Assert.Equal(ComponentAContent, await LoadContentAsync(database.ConnectionString));
    }

    [Fact]
    public async Task ConditionalSave_ArchivedContentIsTheFallbackVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentAContent);
        await using (var context = CreateContext(database.ConnectionString))
        {
            await context.Set<PageBuilderPageEntity>().Where(x => x.Id == SourcePageId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Archived"), TestContext.Current.CancellationToken);
        }
        var group = await LoadGroupAsync(database.ConnectionString);
        await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent,
            PageBuilderContentVersion.Create(group, ComponentAContent));
        Assert.Equal(ComponentBContent, await LoadContentAsync(database.ConnectionString));
    }

    [Fact]
    public async Task ConditionalSave_EmptyDraftDoesNotFallBackToPublishedVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentAContent);
        await SaveRawAsync(database.ConnectionString, PageId, "");
        var group = await LoadGroupAsync(database.ConnectionString);
        await Assert.ThrowsAsync<PageBuilderContentConflictException>(() => SaveConditionalAsync(database.ConnectionString,
            group, ComponentBContent, PageBuilderContentVersion.Create(group, ComponentAContent)));
        await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent,
            PageBuilderContentVersion.Create(group, ""));
        Assert.Equal(ComponentBContent, await LoadContentAsync(database.ConnectionString));
    }

    private static async Task<GroupedPageBuilderPage> LoadGroupAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        var entity = await context.Set<GroupedPageBuilderPageEntity>().SingleAsync(x => x.Id == GroupId, TestContext.Current.CancellationToken);
        return new GroupedPageBuilderPage { Id = entity.Id, StoreId = entity.StoreId, CreatedDate = entity.CreatedDate };
    }

    private static async Task<PageBuilderConditionalContentWriteResult> SaveConditionalAsync(
        string connectionString, GroupedPageBuilderPage group, string content, string version,
        Func<CancellationToken, Task> afterIndex = null)
    {
        using var cache = new TestPlatformMemoryCache();
        var service = new GroupedPageService(
            () => new IndexCallbackRepository(CreateContext(connectionString), afterIndex),
            () => new SqliteContentStreamRepository(CreateContext(connectionString)),
            cache, new NoopEventPublisher(), NullLogger<GroupedPageService>.Instance);
        return await service.SaveGroupContentAsync(group, content, version, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SavePageContentAsync_ConcurrentWritersKeepRawContentAndReferenceIndexOnSameVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var firstWriterReachedCommitBoundary = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriterReachedCommitBoundary = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstWrite = SaveAsync(
            database.ConnectionString,
            ComponentAContent,
            async cancellationToken =>
            {
                firstWriterReachedCommitBoundary.SetResult();
                await releaseFirstWriter.Task.WaitAsync(cancellationToken);
            });
        await firstWriterReachedCommitBoundary.Task.WaitAsync(TestContext.Current.CancellationToken);

        var secondWrite = Task.Run(
            () => SaveAsync(
                database.ConnectionString,
                ComponentBContent,
                cancellationToken =>
                {
                    secondWriterReachedCommitBoundary.SetResult();
                    return Task.CompletedTask;
                }),
            TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(secondWriterReachedCommitBoundary.Task.IsCompleted);

        releaseFirstWriter.SetResult();
        await Task.WhenAll(firstWrite, secondWrite);

        Assert.Equal(ComponentBContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal(
            [ComponentBId],
            await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal(
            [AssetBUrl],
            await LoadAssetUrlsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task SavePageContentAsync_IndexFailureRollsBackRawContentAndReferencesTogether()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);

        await Assert.ThrowsAsync<IOException>(() => SaveAsync(
            database.ConnectionString,
            ComponentBContent,
            _ => throw new IOException("simulated index failure")));

        Assert.Equal(ComponentAContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal(
            [ComponentAId],
            await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal(
            [AssetAUrl],
            await LoadAssetUrlsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task CopyPageContentAsync_UpdatesRawContentAndReferencesInOneTransaction()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentBContent);

        await CopyAsync(database.ConnectionString);

        Assert.Equal(ComponentBContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal([ComponentBId], await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal([AssetBUrl], await LoadAssetUrlsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task CopyPageContentAsync_IndexFailureRollsBackRawContentAndReferencesTogether()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentBContent);

        await Assert.ThrowsAsync<IOException>(() => CopyAsync(
            database.ConnectionString,
            _ => throw new IOException("simulated index failure")));

        Assert.Equal(ComponentAContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal([ComponentAId], await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal([AssetAUrl], await LoadAssetUrlsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task SavePageContentAsync_PageAssetIndexContainsRawAssetsOnly()
    {
        await using var database = await TestDatabase.CreateAsync();

        await SaveAsync(database.ConnectionString, ComponentAContent);

        Assert.Equal([AssetAUrl], await LoadAssetUrlsAsync(database.ConnectionString));
        Assert.DoesNotContain(ComponentEmbeddedAssetUrl, await LoadAssetUrlsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task SavePageContentAsync_IndexingDoesNotReadGroupAfterPageLock()
    {
        await using var database = await TestDatabase.CreateAsync();
        var interceptor = new LockCommandRecorder();
        await using var context = CreateContext(database.ConnectionString, interceptor);
        await context.Set<PageBuilderPageEntity>()
            .Where(x => x.Id == PageId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.StoreId, (string)null),
                TestContext.Current.CancellationToken);
        interceptor.LockedTables.Clear();
        var repository = new SqliteContentStreamRepository(context);

        await repository.SavePageContentAsync(
            PageId,
            ComponentAContent,
            TestContext.Current.CancellationToken);

        Assert.False(interceptor.ReadGroupedPageAfterPageLock);
        Assert.Equal([ComponentAId], await LoadReferenceIdsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task ComponentSave_AcquiresOnlyComponentRelationalLock()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);

        var interceptor = new LockCommandRecorder();
        var contentService = new PageBuilderSharedComponentContentService(
            () => new PageBuilderModuleRepository(CreateContext(database.ConnectionString, interceptor)),
            new NoopEventPublisher(),
            new PageBuilderSharedComponentAssetReferenceIndexService());
        await contentService.SaveContentAsync(
            ComponentAId,
            UpdatedComponentContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(["PageBuilderSharedComponent"], interceptor.LockedTables);

        await SaveAsync(database.ConnectionString, UpdatedPageContent);

        Assert.Equal(UpdatedPageContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal([ComponentAId], await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal([AssetBUrl], await LoadAssetUrlsAsync(database.ConnectionString));

        await using var context = CreateContext(database.ConnectionString);
        Assert.Equal(
            UpdatedComponentContent,
            await context.Set<PageBuilderSharedComponentContentEntity>()
                .Where(x => x.Id == ComponentAId)
                .Select(x => x.ComponentContent)
                .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            [UpdatedComponentAssetUrl],
            await context.Set<PageBuilderSharedComponentAssetReferenceEntity>()
                .Where(x => x.SharedComponentId == ComponentAId)
                .Select(x => x.NormalizedAssetUrl)
                .ToArrayAsync(TestContext.Current.CancellationToken));

        var assetService = new PageBuilderAssetReferenceService(
            () => new PageBuilderModuleRepository(CreateContext(database.ConnectionString)));
        var assetResult = await assetService.SearchReferencesAsync(
            new PageBuilderAssetReferencesSearchCriteria
            {
                StoreId = StoreId,
                AssetUrls = [UpdatedComponentAssetUrl],
                IncludePages = true,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(GroupId, Assert.Single(Assert.Single(assetResult.Results).Pages).Id);
    }

    private static async Task SaveAsync(
        string connectionString,
        string content,
        Func<CancellationToken, Task> afterIndex = null)
    {
        await using var context = CreateContext(connectionString);
        var repository = new SqliteContentStreamRepository(context, afterIndex);
        await repository.SavePageContentAsync(
            PageId,
            content,
            TestContext.Current.CancellationToken);
    }

    private static async Task SaveRawAsync(string connectionString, string pageId, string content)
    {
        await using var context = CreateContext(connectionString);
        var repository = new SqliteContentStreamRepository(context);
        using var reader = new StringReader(content);
        await repository.SaveRawContentAsync(pageId, reader, TestContext.Current.CancellationToken);
    }

    private static async Task CopyAsync(
        string connectionString,
        Func<CancellationToken, Task> afterIndex = null)
    {
        await using var context = CreateContext(connectionString);
        var repository = new SqliteContentStreamRepository(context, afterIndex);
        await repository.CopyPageContentAsync(
            SourcePageId,
            PageId,
            TestContext.Current.CancellationToken);
    }

    private static async Task<string> LoadContentAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        var repository = new SqliteContentStreamRepository(context);
        using var writer = new StringWriter();
        var found = await repository.TryLoadBinaryAsync(PageId, writer, TestContext.Current.CancellationToken);
        Assert.True(found);
        return writer.ToString();
    }

    private static async Task<string[]> LoadReferenceIdsAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        return await context.Set<PageBuilderSharedComponentReferenceEntity>()
            .Where(x => x.PageId == PageId)
            .OrderBy(x => x.SharedComponentId)
            .Select(x => x.SharedComponentId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string[]> LoadAssetUrlsAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        return await context.Set<PageBuilderAssetReferenceEntity>()
            .Where(x => x.PageId == PageId)
            .OrderBy(x => x.NormalizedAssetUrl)
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private static PageBuilderModuleDbContext CreateContext(
        string connectionString,
        IInterceptor interceptor = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseSqlite(connectionString);
        if (interceptor != null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        var options = optionsBuilder.Options;
        return new PageBuilderModuleDbContext(options);
    }

    private sealed class LockCommandRecorder : DbCommandInterceptor
    {
        public IList<string> LockedTables { get; } = [];
        public bool ReadGroupedPageAfterPageLock { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (LockedTables.Contains("PageBuilderPage") &&
                command.CommandText.Contains("GroupedPageBuilderPage", StringComparison.Ordinal))
            {
                ReadGroupedPageAfterPageLock = true;
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE \"PageBuilderPage\"", StringComparison.Ordinal))
            {
                LockedTables.Add("PageBuilderPage");
            }
            else if (command.CommandText.Contains("UPDATE \"PageBuilderSharedComponent\"", StringComparison.Ordinal))
            {
                LockedTables.Add("PageBuilderSharedComponent");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class NoopEventPublisher : IEventPublisher
    {
        public Task Publish<T>(T @event, CancellationToken cancellationToken = default)
            where T : IEvent => Task.CompletedTask;
    }



    private sealed class IndexCallbackRepository(PageBuilderModuleDbContext context, Func<CancellationToken, Task> afterIndex)
        : PageBuilderModuleRepository(context)
    {
        public override async Task RebuildPageContentIndexesAsync(string pageId, string content, string storeId, CancellationToken cancellationToken = default)
        {
            await base.RebuildPageContentIndexesAsync(pageId, content, storeId, cancellationToken);
            if (afterIndex != null)
                await afterIndex(cancellationToken);
        }
    }

    private sealed class SqliteContentStreamRepository(
        PageBuilderModuleDbContext dbContext,
        Func<CancellationToken, Task> afterIndex = null)
        : ContentStreamRepository(dbContext)
    {
        protected override string QuoteOpen => "\"";
        protected override string QuoteClose => "\"";

        protected override string AppendContentChunkSql =>
            $"UPDATE {Table} SET {ContentColumn} = {ContentColumn} || @chunk WHERE {IdColumn} = @id";

        protected override async Task RebuildIndexesAfterRawContentWriteAsync(
            string pageId,
            string content,
            string groupStoreId,
            CancellationToken cancellationToken)
        {
            await base.RebuildIndexesAfterRawContentWriteAsync(
                pageId,
                content,
                groupStoreId,
                cancellationToken);
            if (afterIndex != null)
            {
                await afterIndex(cancellationToken);
            }
        }

        protected override void SetIdParameter(DbCommand cmd, string value)
        {
            cmd.Parameters.Add(new SqliteParameter("@id", value));
        }

        protected override void SetContentChunk(DbCommand cmd, string chunk)
        {
            cmd.Parameters.Add(new SqliteParameter("@chunk", chunk));
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

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"page-builder-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 10,
            }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync(TestContext.Current.CancellationToken);

            await using var context = CreateContext(connectionString);
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var now = DateTime.UtcNow;
            context.Add(new GroupedPageBuilderPageEntity
            {
                Id = GroupId,
                StoreId = StoreId,
                CreatedDate = now,
            });
            context.Add(new PageBuilderPageEntity
            {
                Id = PageId,
                GroupId = GroupId,
                StoreId = StoreId,
                Status = "Draft",
                CreatedDate = now,
            });
            context.Add(new PageBuilderPageEntity
            {
                Id = SourcePageId,
                GroupId = GroupId,
                StoreId = StoreId,
                Status = "Published",
                CreatedDate = now,
            });
            context.AddRange(
                CreateComponent(ComponentAId, now),
                CreateComponent(ComponentBId, now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            return new TestDatabase(connectionString, anchorConnection);
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }

        private static PageBuilderSharedComponentEntity CreateComponent(string id, DateTime createdDate)
        {
            return new PageBuilderSharedComponentEntity
            {
                Id = id,
                StoreId = StoreId,
                Name = id,
                CreatedDate = createdDate,
                Content = new PageBuilderSharedComponentContentEntity
                {
                    Id = id,
                    ComponentContent = id == ComponentAId
                        ? $"{{ \"settings\": {{ \"image\": \"{ComponentEmbeddedAssetUrl}\" }}, \"content\": [] }}"
                        : "{ \"settings\": {}, \"content\": [] }",
                },
            };
        }
    }

    private const string StoreId = "store";
    private const string GroupId = "group";
    private const string PageId = "page";
    private const string SourcePageId = "source-page";
    private const string ComponentAId = "component-a";
    private const string ComponentBId = "component-b";
    private const string AssetAUrl = "/stores/store/a.png";
    private const string AssetBUrl = "/stores/store/b.png";
    private const string ComponentEmbeddedAssetUrl = "/stores/store/component-a.png";
    private const string UpdatedComponentAssetUrl = "/stores/store/component-a-updated.png";
    private const string ComponentAContent = "{ \"settings\": { \"image\": \"/stores/store/a.png\" }, \"content\": [{ \"id\": \"placement-a\", \"type\": \"componentRef\", \"componentRef\": \"component-a\" }] }";
    private const string ComponentBContent = "{ \"settings\": { \"image\": \"/stores/store/b.png\" }, \"content\": [{ \"id\": \"placement-b\", \"type\": \"componentRef\", \"componentRef\": \"component-b\" }] }";
    private const string UpdatedPageContent = "{ \"settings\": { \"image\": \"/stores/store/b.png\" }, \"content\": [{ \"id\": \"placement-a-updated\", \"type\": \"componentRef\", \"componentRef\": \"component-a\" }] }";
    private const string UpdatedComponentContent = "{ \"settings\": { \"image\": \"/stores/store/component-a-updated.png\" }, \"content\": [] }";
}
