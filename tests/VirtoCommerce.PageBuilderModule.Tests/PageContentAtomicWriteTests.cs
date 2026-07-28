using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageContentAtomicWriteTests
{
    [Fact]
    public async Task SaveBinaryAsync_ConcurrentWritersKeepRawContentAndReferenceIndexOnSameVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var firstWriterReachedCommitBoundary = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriterReachedCommitBoundary = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstWrite = SaveAsync(
            database.ConnectionString,
            ComponentAContent,
            async (dbContext, cancellationToken) =>
            {
                await RebuildIndexesAsync(dbContext, ComponentAContent, cancellationToken);
                firstWriterReachedCommitBoundary.SetResult();
                await releaseFirstWriter.Task.WaitAsync(cancellationToken);
            });
        await firstWriterReachedCommitBoundary.Task.WaitAsync(TestContext.Current.CancellationToken);

        var secondWrite = Task.Run(
            () => SaveAsync(
                database.ConnectionString,
                ComponentBContent,
                async (dbContext, cancellationToken) =>
                {
                    await RebuildIndexesAsync(dbContext, ComponentBContent, cancellationToken);
                    secondWriterReachedCommitBoundary.SetResult();
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
    public async Task SaveBinaryAsync_IndexFailureRollsBackRawContentAndReferencesTogether()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(
            database.ConnectionString,
            ComponentAContent,
            (dbContext, cancellationToken) => RebuildIndexesAsync(dbContext, ComponentAContent, cancellationToken));

        await Assert.ThrowsAsync<IOException>(() => SaveAsync(
            database.ConnectionString,
            ComponentBContent,
            async (dbContext, cancellationToken) =>
            {
                await RebuildIndexesAsync(dbContext, ComponentBContent, cancellationToken);
                throw new IOException("simulated index failure");
            }));

        Assert.Equal(ComponentAContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal(
            [ComponentAId],
            await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal(
            [AssetAUrl],
            await LoadAssetUrlsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task CopyContentAsync_UpdatesRawContentAndReferencesInOneTransaction()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(
            database.ConnectionString,
            ComponentAContent,
            (dbContext, cancellationToken) => RebuildIndexesAsync(dbContext, ComponentAContent, cancellationToken));
        await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentBContent);

        await CopyAsync(
            database.ConnectionString,
            async (dbContext, cancellationToken) =>
            {
                var copiedContent = await LoadContentAsync(dbContext, PageId, cancellationToken);
                await RebuildIndexesAsync(dbContext, copiedContent, cancellationToken);
            });

        Assert.Equal(ComponentBContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal([ComponentBId], await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal([AssetBUrl], await LoadAssetUrlsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task CopyContentAsync_IndexFailureRollsBackRawContentAndReferencesTogether()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(
            database.ConnectionString,
            ComponentAContent,
            (dbContext, cancellationToken) => RebuildIndexesAsync(dbContext, ComponentAContent, cancellationToken));
        await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentBContent);

        await Assert.ThrowsAsync<IOException>(() => CopyAsync(
            database.ConnectionString,
            async (dbContext, cancellationToken) =>
            {
                var copiedContent = await LoadContentAsync(dbContext, PageId, cancellationToken);
                await RebuildIndexesAsync(dbContext, copiedContent, cancellationToken);
                throw new IOException("simulated index failure");
            }));

        Assert.Equal(ComponentAContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal([ComponentAId], await LoadReferenceIdsAsync(database.ConnectionString));
        Assert.Equal([AssetAUrl], await LoadAssetUrlsAsync(database.ConnectionString));
    }

    private static async Task SaveAsync(
        string connectionString,
        string content,
        Func<PageBuilderModuleDbContext, CancellationToken, Task> updateIndexAsync)
    {
        await using var context = CreateContext(connectionString);
        var repository = new SqliteContentStreamRepository(context);
        using var reader = new StringReader(content);
        await repository.SaveBinaryAsync(
            PageId,
            reader,
            updateIndexAsync,
            TestContext.Current.CancellationToken);
    }

    private static async Task SaveRawAsync(string connectionString, string pageId, string content)
    {
        await using var context = CreateContext(connectionString);
        var repository = new SqliteContentStreamRepository(context);
        using var reader = new StringReader(content);
        await repository.SaveBinaryAsync(pageId, reader, TestContext.Current.CancellationToken);
    }

    private static async Task CopyAsync(
        string connectionString,
        Func<PageBuilderModuleDbContext, CancellationToken, Task> updateIndexAsync)
    {
        await using var context = CreateContext(connectionString);
        var repository = new SqliteContentStreamRepository(context);
        await repository.CopyContentAsync(
            SourcePageId,
            PageId,
            updateIndexAsync,
            TestContext.Current.CancellationToken);
    }

    private static async Task RebuildIndexesAsync(
        PageBuilderModuleDbContext dbContext,
        string content,
        CancellationToken cancellationToken)
    {
        await PageBuilderLinkedComponentReferenceIndexService.RebuildPageIndexInCurrentTransactionAsync(
            dbContext,
            PageId,
            content,
            cancellationToken);
        await PageBuilderAssetReferenceIndexService.RebuildPageIndexInCurrentTransactionAsync(
            dbContext,
            PageId,
            content,
            cancellationToken);
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

    private static Task<string> LoadContentAsync(
        PageBuilderModuleDbContext dbContext,
        string pageId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<PageBuilderContentEntity>()
            .AsNoTracking()
            .Where(x => x.Id == pageId)
            .Select(x => x.PageContent)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<string[]> LoadReferenceIdsAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        return await context.Set<PageBuilderLinkedComponentReferenceEntity>()
            .Where(x => x.PageId == PageId)
            .OrderBy(x => x.LinkedComponentId)
            .Select(x => x.LinkedComponentId)
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

    private static PageBuilderModuleDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new PageBuilderModuleDbContext(options);
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

        private static PageBuilderLinkedComponentEntity CreateComponent(string id, DateTime createdDate)
        {
            return new PageBuilderLinkedComponentEntity
            {
                Id = id,
                StoreId = StoreId,
                Name = id,
                CreatedDate = createdDate,
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = id,
                    ComponentContent = "{ \"settings\": {}, \"content\": [] }",
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
    private const string ComponentAContent = "{ \"settings\": { \"image\": \"/stores/store/a.png\" }, \"content\": [{ \"id\": \"placement-a\", \"type\": \"componentRef\", \"componentRef\": \"component-a\" }] }";
    private const string ComponentBContent = "{ \"settings\": { \"image\": \"/stores/store/b.png\" }, \"content\": [{ \"id\": \"placement-b\", \"type\": \"componentRef\", \"componentRef\": \"component-b\" }] }";
}
