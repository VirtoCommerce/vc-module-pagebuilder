using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetReferenceServiceSharedComponentTests
{
    [Fact]
    public async Task SearchReferencesAsync_UnionsDirectAndComponentDerivedPagesWithoutDoubleCounting()
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
        Assert.Equal(1, sharedReference.SharedComponentReferencesCount);
        Assert.Equal(2, sharedReference.ReferencesCount);
        Assert.Equal(GroupId, Assert.Single(sharedReference.Pages).Id);
        Assert.Equal(ComponentId, Assert.Single(sharedReference.SharedComponents).Id);

        var componentOnlyReference = Assert.Single(
            result.Results,
            x => x.AssetUrl == ComponentOnlyAssetUrl);
        Assert.Equal(1, componentOnlyReference.PageReferencesCount);
        Assert.Equal(1, componentOnlyReference.SharedComponentReferencesCount);
        Assert.Equal(2, componentOnlyReference.ReferencesCount);
        Assert.Equal(GroupId, Assert.Single(componentOnlyReference.Pages).Id);
        var componentOwner = Assert.Single(componentOnlyReference.SharedComponents);
        Assert.Equal(ComponentId, componentOwner.Id);
        Assert.Equal("Shared component", componentOwner.Name);
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
        Assert.Equal(1, reference.SharedComponentReferencesCount);
        Assert.Equal(2, reference.ReferencesCount);
        Assert.Single(reference.Pages);
        Assert.Single(reference.SharedComponents);
    }

    [Fact]
    public async Task SearchReferencesAsync_ReflectsComponentAssetChangeWithoutPageFanout()
    {
        await using var database = await TestDatabase.CreateAsync();
        var contentService = new PageBuilderSharedComponentContentService(
            database.RepositoryFactory,
            new NoopEventPublisher());
        await contentService.SaveContentAsync(
            ComponentId,
            $"{{ \"settings\": {{ \"image\": \"{ReplacementAssetUrl}\" }}, \"content\": [] }}",
            TestContext.Current.CancellationToken);

        var service = new PageBuilderAssetReferenceService(database.RepositoryFactory);
        var result = await service.SearchReferencesAsync(
            new PageBuilderAssetReferencesSearchCriteria
            {
                StoreId = StoreId,
                IncludePages = true,
                AssetUrls = [ComponentOnlyAssetUrl, ReplacementAssetUrl],
            },
            TestContext.Current.CancellationToken);

        var oldReference = Assert.Single(result.Results, x => x.AssetUrl == ComponentOnlyAssetUrl);
        Assert.Equal(0, oldReference.ReferencesCount);
        Assert.Empty(oldReference.Pages);
        Assert.Empty(oldReference.SharedComponents);

        var replacementReference = Assert.Single(result.Results, x => x.AssetUrl == ReplacementAssetUrl);
        Assert.Equal(1, replacementReference.PageReferencesCount);
        Assert.Equal(1, replacementReference.SharedComponentReferencesCount);
        Assert.Equal(GroupId, Assert.Single(replacementReference.Pages).Id);
        Assert.Equal(ComponentId, Assert.Single(replacementReference.SharedComponents).Id);
    }

    private sealed class NoopEventPublisher : IEventPublisher
    {
        public Task Publish<T>(T @event, System.Threading.CancellationToken cancellationToken = default)
            where T : IEvent => Task.CompletedTask;
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
            context.Add(new PageBuilderSharedComponentEntity
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Shared component",
                CreatedDate = createdDate,
                Content = new PageBuilderSharedComponentContentEntity
                {
                    Id = ComponentId,
                    ComponentContent = "{ \"settings\": {}, \"content\": [] }",
                },
            });
            context.AddRange(
                CreateComponentReference(SharedAssetUrl),
                CreateComponentReference(ComponentOnlyAssetUrl));
            context.Add(new PageBuilderSharedComponentReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                PageId = PageId,
                SharedComponentId = ComponentId,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            return database;
        }

        private static PageBuilderSharedComponentAssetReferenceEntity CreateComponentReference(string assetUrl)
        {
            return new PageBuilderSharedComponentAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                SharedComponentId = ComponentId,
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
    private const string ReplacementAssetUrl = "/stores/store/replacement.png";
}
