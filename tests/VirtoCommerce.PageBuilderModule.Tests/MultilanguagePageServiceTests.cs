using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

// Grouped saves expire the static grouped-page cache regions; running in parallel with tests that assert
// on those regions (e.g. GroupedPageStoreMoveConcurrencyTests) makes them flaky.
[Collection(nameof(MultilanguagePageServiceTests))]
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

    [Fact]
    public async Task GetLanguagesAsync_ListsStoreLanguagesDefaultFirst_ThenRemovedLanguages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        await fixture.CreateLanguageAsync(root, "fr-FR", permalink: "/a-propos", overriddenFields: "Permalink", draftContent: Content("fr"));

        var languages = await fixture.Roots.GetLanguagesAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("en-US", true, true, MultilanguagePageLanguage.Missing),
                ("nl-NL", false, true, "Draft"),
                ("de-DE", false, true, MultilanguagePageLanguage.Missing),
                ("fr-FR", false, false, "Draft"),
            ],
            languages.Select(x => (x.CultureName, x.IsDefault, x.IsStoreLanguage, x.Status)));
        Assert.Null(languages[0].Page);
        Assert.NotNull(languages[1].Page);
    }

    [Fact]
    public async Task CreateLanguageAsync_CopyContent_StartsFromDefaultLanguageWithOwnSettings()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("hero-en"));

        var nl = await fixture.Roots.CreateLanguageAsync(root.Id, "nl-NL", copyContent: true, TestContext.Current.CancellationToken);

        var content = (await fixture.Groups.LoadGroupContentAsync(nl, draft: true, TestContext.Current.CancellationToken)).Content;
        Assert.Contains("hero-en", content);
        Assert.Contains("\"cultureName\":\"nl-NL\"", content);
        var entity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Equal((root.Id, root.Permalink, string.Empty), (entity.MultilanguagePageId, entity.Permalink, entity.OverriddenFields));
    }

    [Fact]
    public async Task CreateLanguageAsync_NotAStoreLanguageOrAlreadyPresent_Throws()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Roots.CreateLanguageAsync(root.Id, "fr-FR", copyContent: false, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Roots.CreateLanguageAsync(root.Id, "nl-NL", copyContent: false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAllAsync_PublishesDraftsAndSkipsVersionsWithoutDraft()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(root, "nl-NL");

        var result = await fixture.Roots.PublishAllAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(["en-US"], result.ProcessedCultureNames);
        Assert.Equal([("nl-NL", MultilanguagePageBatchResult.NoDraftReason)], result.Skipped.Select(x => (x.CultureName, x.Reason)));
        Assert.Equal("Published", (await fixture.Groups.GetByIdAsync(en.Id)).Status);
    }

    [Fact]
    public async Task ArchiveAllAsync_ArchivesEveryVersion()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));

        var result = await fixture.Roots.ArchiveAllAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(["en-US", "nl-NL"], result.ProcessedCultureNames.Order());
        Assert.Equal("Archived", (await fixture.Groups.GetByIdAsync(en.Id)).Status);
        Assert.Equal("Archived", (await fixture.Groups.GetByIdAsync(nl.Id)).Status);
    }

    [Fact]
    public void ToPageDocument_LanguageVersion_CarriesTranslationGroup()
    {
        var group = new GroupedPageBuilderPage { Id = "group", CultureName = "nl-NL", MultilanguagePageId = "root" };
        var page = new PageBuilderPage { Id = "page", GroupId = "group" };

        var document = page.ToPageDocument(group, Content("nl"));

        Assert.Equal("root", document.TranslationGroupId);
    }

    [Fact]
    public async Task List_MultilanguagePageIsOneRowWithLanguages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        var landing = await fixture.CreateLanguageAsync(null, "en-US", permalink: "/summer", name: "Summer", draftContent: Content("summer"));

        var result = await fixture.List.SearchAsync(new PageBuilderPageSearchCriteria { StoreId = StoreId, Take = 10 }, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.TotalCount);
        var multilanguage = result.Results.Single(x => x.Type == "Multilanguage");
        Assert.Equal(root.Id, multilanguage.Id);
        Assert.Equal(
            [("en-US", "Draft"), ("nl-NL", "Draft"), ("de-DE", MultilanguagePageLanguage.Missing)],
            multilanguage.Languages.Select(x => (x.CultureName, x.Status)));
        var landingRow = result.Results.Single(x => x.Type == "Landing");
        Assert.Equal(landing.Id, landingRow.Page.Id);
        Assert.Null(landingRow.Languages);
    }

    [Fact]
    public async Task List_VersionFilter_ListsRootWhenAnyLanguageMatches()
    {
        await using var fixture = await Fixture.CreateAsync();
        var withDraft = await fixture.CreateRootAsync("With draft");
        await fixture.CreateLanguageAsync(withDraft, "nl-NL", draftContent: Content("nl"));
        var archived = await fixture.CreateRootAsync("Archived");
        var archivedEn = await fixture.CreateLanguageAsync(archived, "en-US", draftContent: Content("en"));
        await fixture.Roots.ArchiveAllAsync(archived.Id, TestContext.Current.CancellationToken);
        await fixture.CreateRootAsync("No languages");

        var result = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Lifecycle = "drafts", Take = 10 },
            TestContext.Current.CancellationToken);

        Assert.Equal([withDraft.Id], result.Results.Select(x => x.Id));
        Assert.NotNull(archivedEn);
    }

    [Fact]
    public async Task List_NoVersionFilter_ListsRootWithoutLanguages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync("No languages");

        var result = await fixture.List.SearchAsync(new PageBuilderPageSearchCriteria { StoreId = StoreId, Take = 10 }, TestContext.Current.CancellationToken);

        Assert.Equal([root.Id], result.Results.Select(x => x.Id));
    }

    [Fact]
    public async Task List_SortsAndPagesAcrossBothTypes_IgnoresUnknownSortColumn()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreateRootAsync("B root");
        await fixture.CreateLanguageAsync(null, "en-US", permalink: "/a", name: "A landing", draftContent: Content("a"));
        await fixture.CreateLanguageAsync(null, "en-US", permalink: "/c", name: "C landing", draftContent: Content("c"));

        var result = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Sort = "name;status", Skip = 1, Take = 2 },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(["B root", "C landing"], result.Results.Select(x => x.Name));
    }

    [Fact]
    public async Task List_PageTypeFilter()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(null, "en-US", permalink: "/a", name: "A landing", draftContent: Content("a"));

        var result = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, PageType = "Multilanguage", Take = 10 },
            TestContext.Current.CancellationToken);

        Assert.Equal([root.Id], result.Results.Select(x => x.Id));
    }

    private static string Content(string sectionType)
    {
        return $$"""{ "settings": {}, "content": [ { "type": "{{sectionType}}" } ] }""";
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
            var storeService = new FakeStoreService(new Store { Id = StoreId, DefaultLanguage = "en-US", Languages = ["en-US", "nl-NL", "de-DE"] });
            Roots = new MultilanguagePageService(
                repositoryFactory,
                _cache,
                events,
                Groups,
                Groups,
                groupSearch,
                storeService,
                new PageBuilderPageService(repositoryFactory, _cache, events));
            List = new PageBuilderPageListService(repositoryFactory, groupSearch, Groups, Roots, storeService);
        }

        public GroupedPageService Groups { get; }

        public MultilanguagePageService Roots { get; }

        public PageBuilderPageListService List { get; }

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

        public async Task<MultilanguagePage> CreateRootAsync(string name = "About us")
        {
            var root = new MultilanguagePage
            {
                StoreId = StoreId,
                Name = name,
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
            string overriddenFields = null,
            string draftContent = null)
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

            if (draftContent != null)
            {
                group.Pages.Add(new PageBuilderPage { StoreId = StoreId, Status = "Draft", Content = draftContent });
            }

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

[CollectionDefinition(nameof(MultilanguagePageServiceTests), DisableParallelization = true)]
public class MultilanguagePageServiceTestsCollection;
