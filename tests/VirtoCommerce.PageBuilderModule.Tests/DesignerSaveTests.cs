using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Common;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;
using Fakes = VirtoCommerce.PageBuilderModule.Tests.PublishedRenameContentPreservationTests;
using Permissions = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class DesignerSaveTests
{
    private const string Catalog = "abc12345";
    private const string Header = "default/sections/header-group.json";
    private const string Rules = "default/config/template_rules.json";
    private const string Template = "default/templates/product.json";
    private const string Published = """{"settings":{},"content":[]}""";
    private const string Edited = """{"settings":{"name":"Home"},"content":[{"id":"a","type":"text"}]}""";
    private const string HeaderFile = "{\n  \"content\": [\n    { \"id\": \"h\", \"type\": \"site-header\", \"since\": \"2026-10-16T12:00:00Z\" }\n  ]\n}";
    private const string RulesFile = """{ "version": 2, "conditions": [] }""";

    private readonly FakeBlobStorage _live = new();
    private readonly Fakes.FakeGroupedPageService _pages = new();
    private readonly RecordingContentHistory _history = new();
    private readonly InProcessDistributedLock _lock = new();
    private readonly ThemeStorageFactory _themes;

    public DesignerSaveTests()
    {
        _themes = new ThemeStorageFactory(_live.StoreService, _live, _live, _live);
        _pages.SeedGroup(new GroupedPageBuilderPage { Id = "g1", StoreId = "s1", Pages = [new PageBuilderPage { Id = "p1", StoreId = "s1", Status = ModuleConstants.PageStatuses.Published }] });
        _pages.SeedContent("p1", Published);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveDesignerChanges_PageAndThemeFiles_WritesThemAllAsOneCommit()
    {
        var github = new GitHubScript(
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => GitHubScript.Json("""{ "object": { "sha": "1111111111111111111111111111111111111111" } }""")),
            ("GET", "repos/acme/site-s1/git/commits/1111111111111111111111111111111111111111", (_, _) => GitHubScript.Json("""{ "tree": { "sha": "t0" } }""")),
            ("POST", "repos/acme/site-s1/git/trees", (_, body) =>
            {
                Assert.Equal(
                    [("database-pages/g1.json", Edited), ($"theme/{Header}", HeaderFile), ($"theme/{Rules}", RulesFile)],
                    JObject.Parse(body!)["tree"]!.Select(x => (x["path"]!.Value<string>(), x["content"]!.Value<string>())));
                return GitHubScript.Json("""{ "sha": "t1" }""", HttpStatusCode.Created);
            }),
            ("POST", "repos/acme/site-s1/git/commits", (_, body) =>
            {
                Assert.EndsWith("VC-Save: save-0001", JObject.Parse(body!)["message"]?.Value<string>());
                return GitHubScript.Json("""{ "sha": "2222222222222222222222222222222222222222" }""", HttpStatusCode.Created);
            }),
            ("PATCH", "repos/acme/site-s1/git/refs/heads/main", (_, _) => GitHubScript.Json("{}")));
        var controller = Builder(history: SiteHistories.Create(github, _themes, _pages));
        controller.HttpContext.Request.Headers[ModuleConstants.Headers.SaveId] = "save-0001";

        var result = await controller.SaveDesignerChanges("s1", Save(DatabasePage(), (Header, HeaderFile), (Rules, RulesFile)));

        github.AssertDone();
        var body = PlatformJson.ParseBody(result.Result);
        Assert.Equal("save-0001", (string)body["saveId"]);
        Assert.Equal(PageBuilderContentVersion.Create(await _pages.GetByIdAsync("g1"), Edited), (string)body["eTag"]);
        Assert.Equal(Edited, await DraftAsync());
        // the files as they were sent: the published version is told by their bytes
        Assert.Equal(HeaderFile, _live.Files[$"{Header}-draft"]);
        Assert.Equal(RulesFile, _live.Files[$"{Rules}-draft"]);
        Assert.Single(_live.Events.OfType<PageBuilderPageChangedEvent>());
        Assert.Equal([Header, Rules], _live.Events.OfType<PageBuilderContentChangedEvent>().Single().ChangedEntries.Select(x => x.NewEntry.Path));
    }

    [Fact]
    public async Task SaveDesignerChanges_Saved_RecordsOneVersionWithEveryPart()
    {
        await Builder().SaveDesignerChanges("s1", Save(DatabasePage(), (Header, HeaderFile)));

        var (storeId, record) = Assert.Single(_history.Records);
        Assert.Equal("s1", storeId);
        Assert.Equal(ModuleConstants.GitHistory.SavedSummary, record.Description);
        Assert.Equal("alice", record.UserName);
        Assert.Equal([("page:g1", Edited), ($"theme:{Header}", HeaderFile)], record.Entries.Select(x => (x.Item.ToString(), x.Content)));
    }

    [Fact]
    public async Task SaveDesignerChanges_PageSavedSinceRead_ReturnsPreconditionFailedAndWritesNothing()
    {
        var stale = DatabasePage();
        stale.ETag = PageBuilderContentVersion.Create(new GroupedPageBuilderPage { Id = "other" }, Published);

        var result = await Builder().SaveDesignerChanges("s1", Save(stale, (Header, HeaderFile)));

        Assert.Equal(StatusCodes.Status412PreconditionFailed, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        AssertNothingWritten();
    }

    [Fact]
    public async Task SaveDesignerChanges_PageRefusedAfterTheThemeFiles_PutsTheirDraftsBack()
    {
        _live.Files[$"{Header}-draft"] = "saved before";
        // the version matched when checked, and someone saved before the write
        _pages.SaveContentException = new PageBuilderContentConflictException();

        var result = await Builder().SaveDesignerChanges("s1", Save(DatabasePage(), (Header, HeaderFile), (Rules, RulesFile)));

        Assert.Equal(StatusCodes.Status412PreconditionFailed, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal("saved before", _live.Files[$"{Header}-draft"]);
        Assert.False(_live.Files.ContainsKey($"{Rules}-draft"));
        Assert.Empty(_live.Events.OfType<PageBuilderContentChangedEvent>());
        Assert.Empty(_history.Records);
    }

    [Fact]
    public async Task SaveDesignerChanges_ThemeFileDoesNotFitTheCatalog_ReturnsBadRequestAndWritesNothing()
    {
        await SaveCatalogAsync();
        const string carousel = """{ "content": [ { "id": "c", "type": "carousel" } ] }""";

        var result = await Builder().SaveDesignerChanges("s1", Save(DatabasePage(), (Header, carousel), (Rules, RulesFile)), Catalog);

        var body = PlatformJson.ParseBody(Assert.IsType<BadRequestObjectResult>(result.Result));
        Assert.Contains("'carousel'", (string)body["errors"]!.Single());
        AssertNothingWritten();
    }

    [Fact]
    public async Task SaveDesignerChanges_ThemeFilesFitTheCatalog_SavesThem()
    {
        await SaveCatalogAsync();

        var result = await Builder().SaveDesignerChanges("s1", Save(null, (Header, HeaderFile), (Rules, RulesFile)), Catalog);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(HeaderFile, _live.Files[$"{Header}-draft"]);
    }

    [Theory]
    [InlineData("default/config/other.json")]
    [InlineData("default/../x.json")]
    [InlineData("header-group.json")]
    public async Task SaveDesignerChanges_PathTheBuilderDoesNotSave_ReturnsBadRequestAndWritesNothing(string path)
    {
        var result = await Builder().SaveDesignerChanges("s1", Save(DatabasePage(), (Header, HeaderFile), (path, "{}")));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        AssertNothingWritten();
    }

    [Theory]
    [InlineData("page", Permissions.Update)]
    [InlineData(Template, Permissions.Templates)]
    [InlineData(Rules, Permissions.Templates)]
    [InlineData(Header, Permissions.Theme)]
    public async Task SaveDesignerChanges_Part_RequiresThePermissionThatEditsIt(string part, string permission)
    {
        var request = part == "page" ? Save(DatabasePage()) : Save(null, (part, """{ "content": [] }"""));
        var others = new[] { Permissions.Read, Permissions.Update, Permissions.Templates, Permissions.Theme }.Where(x => x != permission).ToArray();

        var refused = await Builder(new FakeAuthorizationService("s1", others)).SaveDesignerChanges("s1", request);

        Assert.IsType<ForbidResult>(refused.Result);
        AssertNothingWritten();

        var saved = await Builder(new FakeAuthorizationService("s1", Permissions.Read, permission)).SaveDesignerChanges("s1", request);

        Assert.IsType<OkObjectResult>(saved.Result);
    }

    [Fact]
    public async Task SaveDesignerChanges_StoreUserDoesNotWorkIn_ReturnsForbidBeforeBadRequest()
    {
        var result = await Builder(new FakeAuthorizationService("s2", Permissions.Read, Permissions.Update)).SaveDesignerChanges("s1", new DesignerSaveRequest());

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task SaveDesignerChanges_PageOfAnotherStore_ReturnsForbidBeforeBadRequest()
    {
        _pages.SeedGroup(new GroupedPageBuilderPage { Id = "other", StoreId = "s2", Pages = [] });
        var page = new DesignerSavePage { GroupId = "other", ETag = "\"x\"", Content = Edited };

        var result = await Builder().SaveDesignerChanges("s1", Save(page, ("default/../x.json", "{}")));

        Assert.IsType<ForbidResult>(result.Result);
        AssertNothingWritten();
    }

    [Fact]
    public async Task SaveDesignerChanges_UnknownPage_ReturnsNotFound()
    {
        var page = new DesignerSavePage { GroupId = "gone", ETag = "\"x\"", Content = Edited };

        Assert.IsType<NotFoundResult>((await Builder().SaveDesignerChanges("s1", Save(page, (Header, HeaderFile)))).Result);
        AssertNothingWritten();
    }

    [Fact]
    public async Task SaveDesignerChanges_SnapshotOrCatalog_ReturnsBadRequestAndWritesNothing()
    {
        var result = await Builder().SaveDesignerChanges("s1", Save(null, ("default/snapshots/current.json", "{}"), ("default/catalogs/x.json", "{}")));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        AssertNothingWritten();
    }

    [Fact]
    public async Task DiscardDesignerChanges_PageOfAnotherStore_ReturnsForbidBeforeBadRequest()
    {
        _pages.SeedGroup(new GroupedPageBuilderPage { Id = "other", StoreId = "s2", Pages = [] });

        var result = await Builder().DiscardDesignerChanges("s1", Discard(new DesignerSavePage { GroupId = "other" }, "default/../x.json"));

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task DiscardDesignerChanges_Discarded_AnswersTheSaveIdOnly()
    {
        _live.Files[Header] = "live header";
        _live.Files[$"{Header}-draft"] = HeaderFile;

        var result = await Builder().DiscardDesignerChanges("s1", Discard(null, Header));

        Assert.Equal(["saveId"], PlatformJson.ParseBody(result.Result).Properties().Select(x => x.Name));
    }

    [Fact]
    public async Task SaveDesignerChanges_PageWithoutVersion_ReturnsPreconditionRequired()
    {
        var page = DatabasePage();
        page.ETag = null;

        var result = await Builder().SaveDesignerChanges("s1", Save(page, (Header, HeaderFile)));

        Assert.Equal(StatusCodes.Status428PreconditionRequired, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        AssertNothingWritten();
    }

    [Theory]
    [InlineData("""{"content":[]}""")]
    [InlineData("not json")]
    public async Task SaveDesignerChanges_PageOutsideItsEnvelope_ReturnsBadRequest(string content)
    {
        var page = DatabasePage();
        page.Content = content;

        Assert.IsType<BadRequestObjectResult>((await Builder().SaveDesignerChanges("s1", Save(page, (Header, HeaderFile)))).Result);
        AssertNothingWritten();
    }

    [Fact]
    public async Task SaveDesignerChanges_Nothing_ReturnsBadRequest()
    {
        Assert.IsType<BadRequestObjectResult>((await Builder().SaveDesignerChanges("s1", new DesignerSaveRequest())).Result);
    }

    [Fact]
    public async Task SaveDesignerChanges_FilePageOfAStoreWithoutGit_WritesItsDraftWithTheThemeFiles()
    {
        const string about = """{"settings":{"type":"settings","name":"About"},"content":[{"id":"a","type":"text"}]}""";
        var page = new DesignerSavePage { Path = "about.page", Type = "pages", Content = about };

        var result = await Builder().SaveDesignerChanges("s1", Save(page, (Header, HeaderFile)));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(JToken.Parse(about).ToString(), JToken.Parse(_live.Files["about.page-draft"]).ToString());
        Assert.Equal(HeaderFile, _live.Files[$"{Header}-draft"]);
        // the page's history is the git flow's
        Assert.Equal([$"theme:{Header}"], Assert.Single(_history.Records).Record.Entries.Select(x => x.Item.ToString()));
    }

    [Fact]
    public async Task SaveDesignerChanges_TemplateNamedAsThePage_ReturnsBadRequest()
    {
        var page = new DesignerSavePage { Path = Template, Type = "themes", Content = """{ "content": [] }""" };

        Assert.IsType<BadRequestObjectResult>((await Builder().SaveDesignerChanges("s1", Save(page))).Result);
    }

    [Fact]
    public async Task SaveDesignerChanges_BrandPaletteAndGroups_ArePublishedInTheSnapshot()
    {
        await new SchemaCatalogService(_themes).SaveAsync("s1", $$"""
            {
              "hash": "{{Catalog}}",
              "sections": {
                "site-header": { "settings": [ { "id": "catalog_menu", "type": "menu" }, { "id": "icons_menu", "type": "menu" } ] },
                "announcement-bar": { "settings": [ { "id": "background", "type": "color-token" } ] },
                "site-footer": { "blocks": [ "footer-menu" ] }
              },
              "blocks": { "footer-menu": { "settings": [ { "id": "menu", "type": "menu" } ] } },
              "shared": {}
            }
            """, Ct);
        const string brand = """{ "logoUrl": "/logo.svg", "secondaryLogoUrl": "/logo-dark.svg", "faviconUrl": "/favicon.ico" }""";
        const string header = """
            { "content": [
              { "id": "h", "type": "site-header", "catalog_menu": "Main menu", "icons_menu": "icons" },
              { "id": "a", "type": "announcement-bar", "background": "color_primary_500" }
            ] }
            """;
        const string footer = """{ "content": [ { "id": "f", "type": "site-footer", "blocks": [ { "id": "m", "type": "footer-menu", "menu": "footer-links" } ] } ] }""";

        var saved = await Builder().SaveDesignerChanges("s1", Save(null,
            ("default/config/brand.json", brand),
            ("default/config/palette.json", """{ "color_primary_500": "#f99e24" }"""),
            ("default/config/palette.dark.json", """{ "color_primary_500": "#ffbb5b" }"""),
            (Header, header),
            ("default/sections/footer-group.json", footer)), Catalog);
        var publication = await ThemePublisher().PublishAsync("s1", Catalog, null, Ct);

        Assert.IsType<OkObjectResult>(saved.Result);
        var snapshot = JObject.Parse(_live.Files[Assert.Single(_live.Files.Keys, x => x.StartsWith($"default/snapshots/{publication}/") && !x.EndsWith("/publication.json"))]);
        Assert.Equal(JObject.Parse(brand), snapshot["brand"], JToken.EqualityComparer);
        Assert.Equal(JObject.Parse("""{ "light": { "color_primary_500": "#f99e24" }, "dark": { "color_primary_500": "#ffbb5b" } }"""), snapshot["palette"], JToken.EqualityComparer);
        Assert.Equal(["h", "a"], snapshot["header"]!["content"]!.Select(x => x.Value<string>("id")));
        Assert.Equal("color_primary_500", snapshot["header"]!["content"]![1]!.Value<string>("background"));
        Assert.Equal("m", snapshot["footer"]!["content"]![0]!["blocks"]![0]!.Value<string>("id"));
        Assert.Equal(["footer-links", "icons", "Main menu"], snapshot["menus"]!.Values<string>());
        Assert.DoesNotContain(_live.Files.Keys, x => x.EndsWith("-draft"));
    }

    [Fact]
    public async Task DiscardDesignerChanges_PageAndThemeFiles_DiscardsThemAsOneVersion()
    {
        SeedPageDraft();
        _live.Files[Header] = "live header";
        _live.Files[$"{Header}-draft"] = HeaderFile;
        _live.Files[Rules] = "live rules";
        _live.Files[$"{Rules}-draft"] = RulesFile;
        var controller = Builder();
        controller.HttpContext.Request.Headers[ModuleConstants.Headers.SaveId] = "discard-0001";

        var result = await controller.DiscardDesignerChanges("s1", Discard(new DesignerSavePage { GroupId = "g1" }, Header, Rules));

        Assert.Equal("discard-0001", (string)PlatformJson.ParseBody(result.Result)["saveId"]);
        Assert.Equal(["p1"], (await _pages.GetByIdAsync("g1")).Pages.Select(x => x.Id));
        Assert.DoesNotContain(_live.Files.Keys, x => x.EndsWith("-draft"));
        Assert.Equal([Header, Rules], _live.Events.OfType<PageBuilderContentChangedEvent>().Single().ChangedEntries.Select(x => x.NewEntry.Path));
        var (storeId, record) = Assert.Single(_history.Records);
        Assert.Equal("s1", storeId);
        Assert.Equal(ModuleConstants.GitHistory.DiscardedSummary, record.Description);
        Assert.Equal("discard-0001", record.SaveId);
        Assert.Equal([("page:g1", Published), ($"theme:{Header}", "live header"), ($"theme:{Rules}", "live rules")],
            record.Entries.Select(x => (x.Item.ToString(), x.Content)));
    }

    [Fact]
    public async Task DiscardDesignerChanges_ThemeFileNeverPublished_KeepsItsDraft()
    {
        _live.Files[Header] = "live header";
        _live.Files[$"{Header}-draft"] = HeaderFile;
        _live.Files[$"{Rules}-draft"] = RulesFile;

        var result = await Builder().DiscardDesignerChanges("s1", Discard(null, Header, Rules));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.False(_live.Files.ContainsKey($"{Header}-draft"));
        Assert.Equal(RulesFile, _live.Files[$"{Rules}-draft"]);
        Assert.Equal([$"theme:{Header}"], Assert.Single(_history.Records).Record.Entries.Select(x => x.Item.ToString()));
    }

    [Fact]
    public async Task DiscardDesignerChanges_NoDraft_RecordsNothing()
    {
        _live.Files[Header] = "live header";

        var result = await Builder().DiscardDesignerChanges("s1", Discard(new DesignerSavePage { GroupId = "g1" }, Header));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(_history.Records);
        Assert.Empty(_live.Events.OfType<PageBuilderContentChangedEvent>());
    }

    [Fact]
    public async Task DiscardDesignerChanges_PageNeverPublished_ReturnsBadRequestAndPutsTheThemeDraftsBack()
    {
        _pages.SeedGroup(new GroupedPageBuilderPage { Id = "new", StoreId = "s1", Pages = [new PageBuilderPage { Id = "d1", StoreId = "s1", Status = Draft }] });
        _live.Files[Header] = "live header";
        _live.Files[$"{Header}-draft"] = HeaderFile;

        var result = await Builder().DiscardDesignerChanges("s1", Discard(new DesignerSavePage { GroupId = "new" }, Header));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(HeaderFile, _live.Files[$"{Header}-draft"]);
        Assert.Single((await _pages.GetByIdAsync("new")).Pages);
        Assert.Empty(_live.Events.OfType<PageBuilderContentChangedEvent>());
        Assert.Empty(_history.Records);
    }

    [Theory]
    [InlineData("page", Permissions.Update)]
    [InlineData(Rules, Permissions.Templates)]
    [InlineData(Header, Permissions.Theme)]
    public async Task DiscardDesignerChanges_Part_RequiresThePermissionThatEditsIt(string part, string permission)
    {
        SeedPageDraft();
        _live.Files[part == "page" ? Header : part] = "live";
        _live.Files[$"{(part == "page" ? Header : part)}-draft"] = "draft";
        var request = part == "page" ? Discard(new DesignerSavePage { GroupId = "g1" }) : Discard(null, part);
        var others = new[] { Permissions.Read, Permissions.Update, Permissions.Templates, Permissions.Theme }.Where(x => x != permission).ToArray();

        var refused = await Builder(new FakeAuthorizationService("s1", others)).DiscardDesignerChanges("s1", request);

        Assert.IsType<ForbidResult>(refused.Result);
        Assert.Equal(2, (await _pages.GetByIdAsync("g1")).Pages.Count);
        Assert.Contains(_live.Files.Keys, x => x.EndsWith("-draft"));

        var discarded = await Builder(new FakeAuthorizationService("s1", Permissions.Read, permission)).DiscardDesignerChanges("s1", request);

        Assert.IsType<OkObjectResult>(discarded.Result);
    }

    [Fact]
    public async Task DiscardDesignerChanges_StoreUserDoesNotWorkIn_ReturnsForbidBeforeBadRequest()
    {
        var result = await Builder(new FakeAuthorizationService("s2", Permissions.Read, Permissions.Update)).DiscardDesignerChanges("s1", new DesignerDiscardRequest());

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("default/../x.json")]
    [InlineData("default/config/other.json")]
    public async Task DiscardDesignerChanges_NothingOrAPathTheBuilderDoesNotSave_ReturnsBadRequest(string path)
    {
        var request = path == null ? new DesignerDiscardRequest() : Discard(null, Header, path);

        Assert.IsType<BadRequestObjectResult>((await Builder().DiscardDesignerChanges("s1", request)).Result);
    }

    [Fact]
    public async Task DiscardDesignerChanges_FilePageOfAStoreWithoutGit_RemovesItsDraftWithTheThemeFiles()
    {
        _live.Files["about.page"] = "live about";
        _live.Files["about.page-draft"] = "draft about";
        _live.Files[Header] = "live header";
        _live.Files[$"{Header}-draft"] = HeaderFile;

        var result = await Builder().DiscardDesignerChanges("s1", Discard(new DesignerSavePage { Path = "about.page", Type = "pages" }, Header));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.DoesNotContain(_live.Files.Keys, x => x.EndsWith("-draft"));
        // the page's history is the git flow's
        Assert.Equal([$"theme:{Header}"], Assert.Single(_history.Records).Record.Entries.Select(x => x.Item.ToString()));
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private void AssertNothingWritten()
    {
        Assert.DoesNotContain(_live.Files.Keys, x => x.EndsWith("-draft"));
        Assert.Equal(Published, DraftAsync().GetAwaiter().GetResult());
        Assert.Empty(_history.Records);
    }

    private async Task<string> DraftAsync() => (await _pages.LoadGroupContentAsync(await _pages.GetByIdAsync("g1"), cancellationToken: Ct)).Content;

    private void SeedPageDraft()
    {
        _pages.SeedGroup(new GroupedPageBuilderPage
        {
            Id = "g1",
            StoreId = "s1",
            Pages =
            [
                new PageBuilderPage { Id = "p1", StoreId = "s1", Status = ModuleConstants.PageStatuses.Published },
                new PageBuilderPage { Id = "p2", StoreId = "s1", Status = Draft },
            ],
        });
        _pages.SeedContent("p2", Edited);
    }

    private static DesignerDiscardRequest Discard(DesignerSavePage page, params string[] themeFiles) => new()
    {
        Page = page,
        ThemeFiles = themeFiles,
    };

    private DesignerSavePage DatabasePage() => new()
    {
        GroupId = "g1",
        ETag = PageBuilderContentVersion.Create(_pages.GetByIdAsync("g1").GetAwaiter().GetResult(), Published),
        Content = Edited,
    };

    private static DesignerSaveRequest Save(DesignerSavePage page, params (string Path, string Content)[] themeFiles) => new()
    {
        Page = page,
        ThemeFiles = themeFiles.Select(x => new DesignerSaveFile { Path = x.Path, Content = x.Content }).ToList(),
    };

    private Task<string> SaveCatalogAsync() =>
        new SchemaCatalogService(_themes).SaveAsync("s1",
            $$"""{ "hash": "{{Catalog}}", "sections": { "site-header": {}, "product-info": {}, "text": {} }, "blocks": {}, "shared": {} }""", Ct);

    private ThemePublisher ThemePublisher() =>
        new(_themes, new ThemeSnapshotBuilder(), _live.StoreService, new SchemaCatalogService(_themes),
            _lock, _live, new NoContentHistory(), Options.Create(new ThemeSnapshotOptions()), NullLogger<ThemePublisher>.Instance);

    private PageBuilderController Builder(IAuthorizationService authorization = null, IContentHistory history = null)
    {
        authorization ??= new FakeAuthorizationService(true);
        var options = Options.Create(new GitContentOptions());
        var filePageDrafts = TestServices.FilePageDrafts(_live, options, GitHubConnections.CreateService(options.Value), GitHubConnections.CreatePolicy(options, new FakeSettingsManager(false), new FakeGitContentOwnership()), null, null, new FakeGitContentProvisioner());
        var controller = new PageBuilderController(
            storeService: _live.StoreService,
            pathResolver: _live,
            blobContentStorageProviderFactory: _live,
            publishingService: _live,
            eventPublisher: _live,
            gitContentOptions: options,
            gitHubConnectionService: GitHubConnections.CreateService(options.Value),
            gitContentPolicy: GitHubConnections.CreatePolicy(options, new FakeSettingsManager(false), new FakeGitContentOwnership()),
            gitContentRepository: null,
            gitContentHistory: null,
            gitContentPublisher: null,
            gitLivePageService: null,
            settingsManager: null,
            authorizationService: authorization,
            distributedLock: _lock,
            requestValidator: RequestValidators.Content("s1"),
            contentHistory: history ?? _history,
            themeDraftService: new ThemeDraftService(_themes, _lock, _live, NullLogger<ThemeDraftService>.Instance),
            groupedPageService: _pages,
            sharedComponentPlacementValidator: new PageBuilderSharedComponentPlacementValidator(new NoopSharedComponentReferenceIndexService(), authorization),
            designerContentService: TestServices.Designer(history ?? _history, filePageDrafts, new ThemeDraftService(_themes, _lock, _live, NullLogger<ThemeDraftService>.Instance), _pages, _live, ThemePublisher(), null),
            contentPermissionService: TestServices.Permissions(authorization),
            filePageDraftService: filePageDrafts);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice"), new Claim(ClaimTypes.Email, "alice@acme.test")], "test")),
            },
        };
        return controller;
    }
}
