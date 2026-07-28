using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetReferenceServiceLinkedComponentTests
{
    [Fact]
    public async Task SearchReferencesAsync_UnionsPageAndUnusedLinkedComponentOwners()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new PageBuilderAssetReferenceService(database.RepositoryFactory);
        var criteria = new PageBuilderAssetReferencesSearchCriteria
        {
            StoreId = StoreId,
            IncludePages = true,
            AssetUrls = [SharedAssetUrl, ComponentOnlyAssetUrl],
        };

        var result = await service.SearchReferencesAsync(
            criteria,
            TestContext.Current.CancellationToken);

        var sharedReference = Assert.Single(result.Results, x => x.AssetUrl == SharedAssetUrl);
        Assert.Equal(1, sharedReference.PageReferencesCount);
        Assert.Equal(1, sharedReference.LinkedComponentReferencesCount);
        Assert.Equal(2, sharedReference.ReferencesCount);
        Assert.Equal(GroupId, Assert.Single(sharedReference.Pages).Id);
        Assert.Equal(ComponentId, Assert.Single(sharedReference.LinkedComponents).Id);

        var componentOnlyReference = Assert.Single(
            result.Results,
            x => x.AssetUrl == ComponentOnlyAssetUrl);
        Assert.Equal(0, componentOnlyReference.PageReferencesCount);
        Assert.Equal(1, componentOnlyReference.LinkedComponentReferencesCount);
        Assert.Equal(1, componentOnlyReference.ReferencesCount);
        Assert.Empty(componentOnlyReference.Pages);
        var componentOwner = Assert.Single(componentOnlyReference.LinkedComponents);
        Assert.Equal(ComponentId, componentOwner.Id);
        Assert.Equal("Unused shared component", componentOwner.Name);
    }

    [Fact]
    public async Task SearchReferencesAsync_FolderCountsDistinctPageAndComponentOwners()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new PageBuilderAssetReferenceService(database.RepositoryFactory);
        var criteria = new PageBuilderAssetReferencesSearchCriteria
        {
            StoreId = StoreId,
            IncludePages = true,
            FolderUrl = "/stores/store",
        };

        var result = await service.SearchReferencesAsync(
            criteria,
            TestContext.Current.CancellationToken);

        var reference = Assert.Single(result.Results);
        Assert.Equal(1, reference.PageReferencesCount);
        Assert.Equal(1, reference.LinkedComponentReferencesCount);
        Assert.Equal(2, reference.ReferencesCount);
        Assert.Single(reference.Pages);
        Assert.Single(reference.LinkedComponents);
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
                DataSource = $"page-builder-asset-owners-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
            }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new TestDatabase(connectionString, anchorConnection);

            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var createdDate = DateTime.UtcNow;
            context.Add(new GroupedPageBuilderPageEntity
            {
                Id = GroupId,
                StoreId = StoreId,
                Name = "Homepage",
                CultureName = "en-US",
                CreatedDate = createdDate,
            });
            context.Add(new PageBuilderPageEntity
            {
                Id = PageId,
                GroupId = GroupId,
                StoreId = StoreId,
                Status = "Draft",
                CreatedDate = createdDate,
            });
            context.Add(new PageBuilderAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                PageId = PageId,
                NormalizedAssetUrl = SharedAssetUrl,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(SharedAssetUrl),
            });
            context.Add(new PageBuilderLinkedComponentEntity
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Unused shared component",
                CreatedDate = createdDate,
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = ComponentId,
                    ComponentContent = "{ \"settings\": {}, \"content\": [] }",
                },
            });
            context.AddRange(
                CreateComponentReference(SharedAssetUrl),
                CreateComponentReference(ComponentOnlyAssetUrl));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            return database;
        }

        private static PageBuilderLinkedComponentAssetReferenceEntity CreateComponentReference(string assetUrl)
        {
            return new PageBuilderLinkedComponentAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                LinkedComponentId = ComponentId,
                NormalizedAssetUrl = assetUrl,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(assetUrl),
            };
        }

        private PageBuilderModuleDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
                .UseSqlite(ConnectionString)
                .Options;
            return new PageBuilderModuleDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }
    }

    private const string StoreId = "store";
    private const string GroupId = "group";
    private const string PageId = "page";
    private const string ComponentId = "component";
    private const string SharedAssetUrl = "/stores/store/shared.png";
    private const string ComponentOnlyAssetUrl = "/stores/store/component-only.png";
}
