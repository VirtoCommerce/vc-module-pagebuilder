using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class MultilanguagePageServiceTests
{
    private const string StoreId = "store";

    [Fact]
    public async Task SaveChangesAsync_NewRoot_TakesDefaultCultureFromStore()
    {
        await using var fixture = await Fixture.CreateAsync();

        var root = await fixture.CreateRootAsync();

        Assert.Equal("en-US", root.DefaultCultureName);
    }

    [Fact]
    public async Task SaveChangesAsync_RootChanged_PropagatesToInheritedFieldsOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US");
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", overriddenFields: "Permalink");

        root.Name = "About";
        root.Permalink = "/about";
        root.Visibility = false;
        await fixture.Roots.SaveChangesAsync([root]);

        var enEntity = await fixture.LoadGroupAsync(en.Id);
        var nlEntity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Equal(("About", "/about", false), (enEntity.Name, enEntity.Permalink, enEntity.Visibility));
        Assert.Equal(("About", "/over-ons", false), (nlEntity.Name, nlEntity.Permalink, nlEntity.Visibility));
    }

    [Fact]
    public async Task GroupSave_ClientUnawareOfMultilanguageFields_KeepsLinkAndMask()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", overriddenFields: "Permalink");

        var legacyPayload = await fixture.Groups.GetByIdAsync(nl.Id);
        legacyPayload.MultilanguagePageId = null;
        legacyPayload.OverriddenFields = null;
        await fixture.Groups.SaveChangesAsync([legacyPayload]);

        var entity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Equal(root.Id, entity.MultilanguagePageId);
        Assert.Equal("Permalink", entity.OverriddenFields);
    }

    [Fact]
    public async Task LinkAsync_ExistingLandingPage_KeepsItsDifferingValuesAsOverrides()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var landing = await fixture.CreateLanguageAsync(null, "nl-NL", permalink: "/veelgestelde-vragen", name: "Veelgestelde vragen");

        await fixture.Roots.LinkAsync(root.Id, landing.Id, TestContext.Current.CancellationToken);

        var entity = await fixture.LoadGroupAsync(landing.Id);
        Assert.Equal(root.Id, entity.MultilanguagePageId);
        // The landing page was private and had its own permalink: both differ from the root and stay as they were.
        Assert.Equal("Permalink,Visibility", entity.OverriddenFields);
        Assert.Equal("/veelgestelde-vragen", entity.Permalink);
        Assert.False(entity.Visibility);
        Assert.Equal(root.Name, entity.Name);
    }

    [Fact]
    public async Task LinkAsync_LanguageAlreadyPresent_Throws()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL");
        var second = await fixture.CreateLanguageAsync(null, "nl-NL", permalink: "/faq");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.LinkAsync(root.Id, second.Id, TestContext.Current.CancellationToken));

        Assert.Null((await fixture.LoadGroupAsync(second.Id)).MultilanguagePageId);
    }

    [Fact]
    public async Task UnlinkAsync_KeepsValuesAndClearsMask()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", overriddenFields: "Permalink");

        await fixture.Roots.UnlinkAsync(nl.Id, TestContext.Current.CancellationToken);

        var entity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Null(entity.MultilanguagePageId);
        Assert.Equal(string.Empty, entity.OverriddenFields);
        Assert.Equal("/over-ons", entity.Permalink);
    }

    [Fact]
    public async Task DeleteAsync_DetachesLanguagesAndRemovesRoot()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US");

        await fixture.Roots.DeleteAsync([root.Id]);

        await using var context = fixture.CreateContext();
        Assert.False(await context.Set<MultilanguagePageEntity>().AnyAsync(x => x.Id == root.Id, TestContext.Current.CancellationToken));
        var entity = await fixture.LoadGroupAsync(en.Id);
        Assert.Null(entity.MultilanguagePageId);
        Assert.Equal(root.Permalink, entity.Permalink);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _anchorConnection;
        private readonly string _connectionString;
        private readonly TestPlatformMemoryCache _cache = new();

        private Fixture(string connectionString, SqliteConnection anchorConnection)
        {
            _connectionString = connectionString;
            _anchorConnection = anchorConnection;

            Func<IPageBuilderModuleRepository> repositoryFactory = () => new PageBuilderModuleRepository(CreateContext());
            var events = new NoopEventPublisher();
            Groups = new GroupedPageService(
                repositoryFactory,
                () => throw new InvalidOperationException("Content repository is not used by these tests."),
                _cache,
                events,
                NullLogger<GroupedPageService>.Instance);
            var groupSearch = new GroupedPageSearchService(repositoryFactory, _cache, Groups, Options.Create(new CrudOptions()));
            Roots = new MultilanguagePageService(
                repositoryFactory,
                _cache,
                events,
                Groups,
                Groups,
                groupSearch,
                new FakeStoreService(new Store { Id = StoreId, DefaultLanguage = "en-US" }));
        }

        public GroupedPageService Groups { get; }

        public MultilanguagePageService Roots { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"page-builder-multilanguage-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 10,
            }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync(TestContext.Current.CancellationToken);
            var fixture = new Fixture(connectionString, anchorConnection);

            await using var context = fixture.CreateContext();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return fixture;
        }

        public PageBuilderModuleDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>().UseSqlite(_connectionString).Options;
            return new PageBuilderModuleDbContext(options);
        }

        public async Task<MultilanguagePage> CreateRootAsync()
        {
            var root = new MultilanguagePage
            {
                StoreId = StoreId,
                Name = "About us",
                Permalink = "/about-us",
                Visibility = true,
            };
            await Roots.SaveChangesAsync([root]);
            return root;
        }

        public async Task<GroupedPageBuilderPage> CreateLanguageAsync(
            MultilanguagePage root,
            string cultureName,
            string permalink = null,
            string name = null,
            string overriddenFields = null)
        {
            var group = new GroupedPageBuilderPage
            {
                StoreId = StoreId,
                CultureName = cultureName,
                MultilanguagePageId = root?.Id,
            };

            if (root != null)
            {
                MultilanguagePageInheritance.Apply(root, group);
            }

            group.Name = name ?? group.Name ?? "Landing";
            group.Permalink = permalink ?? group.Permalink;
            group.OverriddenFields = overriddenFields;

            await Groups.SaveChangesAsync([group]);
            return group;
        }

        public async Task<GroupedPageBuilderPageEntity> LoadGroupAsync(string id)
        {
            await using var context = CreateContext();
            return await context.Set<GroupedPageBuilderPageEntity>().AsNoTracking()
                .SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _cache.Dispose();
            await _anchorConnection.DisposeAsync();
        }
    }

    private sealed class NoopEventPublisher : IEventPublisher
    {
        public Task Publish<T>(T @event, System.Threading.CancellationToken cancellationToken = default)
            where T : IEvent
        {
            return Task.CompletedTask;
        }
    }
}
