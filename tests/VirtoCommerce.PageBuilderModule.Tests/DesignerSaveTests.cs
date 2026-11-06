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
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Services;
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

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private void AssertNothingWritten()
    {
        Assert.DoesNotContain(_live.Files.Keys, x => x.EndsWith("-draft"));
        Assert.Equal(Published, DraftAsync().GetAwaiter().GetResult());
        Assert.Empty(_history.Records);
    }

    private async Task<string> DraftAsync() => (await _pages.LoadGroupContentAsync(await _pages.GetByIdAsync("g1"), cancellationToken: Ct)).Content;

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

    private PageBuilderController Builder(IAuthorizationService authorization = null, IContentHistory history = null)
    {
        var options = Options.Create(new GitContentOptions());
        var themePublisher = new ThemePublisher(_themes, new ThemeSnapshotBuilder(), _live.StoreService, new SchemaCatalogService(_themes),
            _lock, _live, new NoContentHistory(), Options.Create(new ThemeSnapshotOptions()), NullLogger<ThemePublisher>.Instance);
        var content = new PageBuilderPageContentService(new Fakes.FakePageBuilderPageService(_pages), _pages, new NoopSharedComponentReferenceIndexService(),
            _live, NullLogger<PageBuilderPageContentService>.Instance);
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
            gitContentProvisioner: new FakeGitContentProvisioner(),
            gitLivePageService: null,
            settingsManager: null,
            authorizationService: authorization ?? new FakeAuthorizationService(true),
            distributedLock: _lock,
            requestValidator: RequestValidators.Content("s1"),
            contentHistory: history ?? _history,
            themePublisher: themePublisher,
            themeSnapshotService: null,
            themeDraftService: new ThemeDraftService(_themes, _lock, _live, NullLogger<ThemeDraftService>.Instance),
            groupedPageService: _pages,
            pageContentService: content);
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
