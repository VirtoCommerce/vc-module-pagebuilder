using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Events;
using Xunit;
using Permissions = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderControllerThemeFilesTests
{
    private const string Path = "default/sections/header-group.json";

    private const string Catalog = "abc12345";

    private readonly FakeBlobStorage _live = new();
    private readonly FakeGitContentOwnership _ownership = new("another-store");
    private readonly FakeSettingsManager _settings = new();
    private readonly InProcessDistributedLock _lock = new();
    private readonly ThemeStorageFactory _themeStorageFactory;

    public PageBuilderControllerThemeFilesTests()
    {
        _themeStorageFactory = new ThemeStorageFactory(_live.StoreService, _live, _live, _live);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveTemplates_ThemeFileOfGitStore_DoesNotCallGitHub()
    {
        Assert.IsType<OkObjectResult>(await Builder().SaveTemplates("s1", "default", HeaderGroup(), draft: true));

        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
        Assert.Empty(_ownership.Asked);
    }

    [Fact]
    public async Task DiscardDraft_ThemeFileOfGitStore_DoesNotCallGitHub()
    {
        _live.Files[Path] = "{}";
        _live.Files[$"{Path}-draft"] = "{}";

        Assert.IsType<NoContentResult>(await Builder().DiscardDraft("s1", Path, "themes", "default"));

        Assert.False(_live.Files.ContainsKey($"{Path}-draft"));
        Assert.Empty(_ownership.Asked);
    }

    [Fact]
    public async Task SaveTemplates_ThemeFile_ReturnsAfterContentChangedEventIsHandled()
    {
        var publisher = new GatedEventPublisher();
        var save = Builder(eventPublisher: publisher).SaveTemplates("s1", "default", HeaderGroup(), draft: true);

        await publisher.Handling.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(save.IsCompleted);

        publisher.Handled.SetResult();
        Assert.IsType<OkObjectResult>(await save);
    }

    [Fact]
    public async Task DiscardDraft_StoreUserDoesNotWorkIn_ReturnsForbidAndKeepsDraft()
    {
        _live.Files[Path] = "{}";
        _live.Files[$"{Path}-draft"] = "{}";

        Assert.IsType<ForbidResult>(await Builder(new FakeAuthorizationService(false)).DiscardDraft("s1", Path, "themes", "default"));

        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
    }

    [Theory]
    [InlineData("default/templates/product.json", Permissions.Templates, Permissions.Theme)]
    [InlineData("default/config/template_rules.json", Permissions.Templates, Permissions.Theme)]
    [InlineData("default/sections/header-group.json", Permissions.Theme, Permissions.Templates)]
    [InlineData("default/config/settings_data.json", Permissions.Theme, Permissions.Templates)]
    public async Task SaveTemplates_ThemeFileWithoutPermissionThatEditsIt_ReturnsForbidAndWritesNothing(string path, string required, string other)
    {
        var refused = await Builder(new FakeAuthorizationService("s1", Permissions.Update, other)).SaveTemplates("s1", "default", ThemeFile(path), draft: true);
        Assert.IsType<ForbidResult>(refused);
        Assert.Empty(_live.Files);

        var saved = await Builder(new FakeAuthorizationService("s1", Permissions.Update, required)).SaveTemplates("s1", "default", ThemeFile(path), draft: true);
        Assert.IsType<OkObjectResult>(saved);
        Assert.True(_live.Files.ContainsKey($"{path}-draft"));
    }

    [Fact]
    public async Task DiscardDraft_ThemeFileWithoutThemePermission_ReturnsForbidAndKeepsDraft()
    {
        _live.Files[Path] = "{}";
        _live.Files[$"{Path}-draft"] = "{}";

        var result = await Builder(new FakeAuthorizationService("s1", Permissions.Update, Permissions.Templates)).DiscardDraft("s1", Path, "themes", "default");

        Assert.IsType<ForbidResult>(result);
        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
    }

    [Fact]
    public async Task SaveTemplates_ThemeFileWithTypeTheCatalogLacks_ReturnsBadRequestWithErrorsAndWritesNothing()
    {
        await SaveCatalogAsync();

        var result = await Builder().SaveTemplates("s1", "default", HeaderGroup("site-header", "carousel"), draft: true, catalogHash: Catalog);

        var body = PlatformJson.ParseBody(Assert.IsType<BadRequestObjectResult>(result));
        Assert.Contains("'carousel'", (string)body["errors"]!.Single());
        Assert.Contains("'carousel'", (string)body["error"]);
        Assert.False(_live.Files.ContainsKey($"{Path}-draft"));
    }

    [Fact]
    public async Task SaveTemplates_UnknownCatalogHash_ReturnsBadRequestAndWritesNothing()
    {
        await SaveCatalogAsync();

        var result = await Builder().SaveTemplates("s1", "default", HeaderGroup("site-header"), draft: true, catalogHash: "missing123");

        var body = PlatformJson.ParseBody(Assert.IsType<BadRequestObjectResult>(result));
        Assert.Contains("missing123", (string)body["errors"]!.Single());
        Assert.False(_live.Files.ContainsKey($"{Path}-draft"));
    }

    [Fact]
    public async Task SaveTemplates_ThemeFilesFitCatalog_SavesDrafts()
    {
        await SaveCatalogAsync();
        var files = new PageBuilderController.SaveFilesModel
        {
            Files = JsonConvert.SerializeObject(new object[]
            {
                new { path = Path, type = "themes", content = Group("site-header") },
                new { path = "default/templates/product.json", type = "themes", content = Group("product-info") },
                new { path = "default/config/template_rules.json", type = "themes", content = new { version = 1 } },
            }),
        };

        Assert.IsType<OkObjectResult>(await Builder().SaveTemplates("s1", "default", files, draft: true, catalogHash: Catalog));

        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
        Assert.True(_live.Files.ContainsKey("default/templates/product.json-draft"));
        Assert.True(_live.Files.ContainsKey("default/config/template_rules.json-draft"));
    }

    [Fact]
    public async Task SaveTemplates_TemplatePlacingSharedComponent_ReturnsBadRequest()
    {
        await SaveCatalogAsync();
        var files = new PageBuilderController.SaveFilesModel
        {
            Files = JsonConvert.SerializeObject(new[]
            {
                new { path = "default/templates/product.json", type = "themes", content = Group("product-info", ModuleConstants.SharedComponents.ReferenceType) },
            }),
        };

        var result = await Builder().SaveTemplates("s1", "default", files, draft: true, catalogHash: Catalog);

        Assert.Contains("shared component", (string)PlatformJson.ParseBody(Assert.IsType<BadRequestObjectResult>(result))["errors"]!.Single());
    }

    [Fact]
    public async Task SaveTemplates_NoCatalogHash_DoesNotCheckTheFiles()
    {
        Assert.IsType<OkObjectResult>(await Builder().SaveTemplates("s1", "default", HeaderGroup("carousel"), draft: true));

        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
    }

    [Fact]
    public async Task SaveTemplates_LiveThemeFileOnNewPageBuilderStore_ReturnsBadRequestAndWritesNothing()
    {
        _settings.Values[ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder.Name] = true;

        var result = await Builder().SaveTemplates("s1", "default", HeaderGroup("site-header"), draft: false);

        Assert.Contains("theme/publish", (string)PlatformJson.ParseBody(Assert.IsType<BadRequestObjectResult>(result))["error"]);
        Assert.Empty(_live.Files);
    }

    [Fact]
    public async Task SaveTemplates_DraftThemeFileOnNewPageBuilderStore_SavesDraft()
    {
        _settings.Values[ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder.Name] = true;

        Assert.IsType<OkObjectResult>(await Builder().SaveTemplates("s1", "default", HeaderGroup("site-header"), draft: true));

        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
        Assert.False(_live.Files.ContainsKey(Path));
    }

    [Fact]
    public async Task SaveTemplates_LiveThemeFileOnOtherStore_WritesLiveFile()
    {
        _settings.Values[ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder.Name] = false;

        Assert.IsType<OkResult>(await Builder().SaveTemplates("s1", "default", HeaderGroup("site-header"), draft: false));

        Assert.True(_live.Files.ContainsKey(Path));
    }

    [Fact]
    public async Task SaveTemplates_LivePageOnNewPageBuilderStore_WritesLivePage()
    {
        _settings.Values[ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder.Name] = true;
        var page = new PageBuilderController.SaveFilesModel
        {
            Files = JsonConvert.SerializeObject(new[] { new { path = "home.page", type = "pages", content = Group("text") } }),
        };

        Assert.IsType<OkResult>(await Builder().SaveTemplates("s1", "default", page, draft: false));

        Assert.True(_live.Files.ContainsKey("home.page"));
    }

    [Fact]
    public async Task SaveTemplates_ThemeTypeInOtherCase_ChecksThemePermission()
    {
        var files = new PageBuilderController.SaveFilesModel
        {
            Files = JsonConvert.SerializeObject(new[] { new { path = Path, type = "Themes", content = Group("site-header") } }),
        };

        var result = await Builder(new FakeAuthorizationService("s1", Permissions.Update, Permissions.Templates)).SaveTemplates("s1", "default", files, draft: true);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(_live.Files);
    }

    [Fact]
    public async Task SaveTemplates_SeveralThemeFiles_RecordsThemAsOneVersion()
    {
        var history = new RecordingContentHistory();
        var files = new PageBuilderController.SaveFilesModel
        {
            Files = JsonConvert.SerializeObject(new object[]
            {
                new { path = Path, type = "themes", content = Group("site-header") },
                new { path = "default/config/template_rules.json", type = "themes", content = new { version = 2 } },
            }),
        };

        await Builder(history: history).SaveTemplates("s1", "default", files, draft: true);

        var (_, record) = Assert.Single(history.Records);
        Assert.Equal(ModuleConstants.GitHistory.SavedSummary, record.Description);
        Assert.Equal([$"theme:{Path}", "theme:default/config/template_rules.json"], record.Entries.Select(x => x.Item.ToString()));
        // the bytes written: they tell the published version later
        Assert.Equal(_live.Files[$"{Path}-draft"], record.Entries[0].Content);
    }

    [Fact]
    public async Task DiscardDraft_ThemeFile_RecordsThePublishedFile()
    {
        var history = new RecordingContentHistory();
        _live.Files[Path] = "published";
        _live.Files[$"{Path}-draft"] = "{}";

        await Builder(history: history).DiscardDraft("s1", Path, "themes", "default");

        var (_, record) = Assert.Single(history.Records);
        Assert.Equal(ModuleConstants.GitHistory.DiscardedSummary, record.Description);
        Assert.Equal([($"theme:{Path}", "published")], record.Entries.Select(x => (x.Item.ToString(), x.Content)));
    }

    private Task<string> SaveCatalogAsync() =>
        new SchemaCatalogService(_themeStorageFactory).SaveAsync("s1",
            $$"""{ "hash": "{{Catalog}}", "sections": { "site-header": {}, "product-info": {}, "text": {} }, "blocks": {}, "shared": {} }""", Ct);

    private static object Group(params string[] types) => new
    {
        settings = new { type = "settings" },
        content = types.Select((type, i) => new { id = $"s{i}", type }).ToArray(),
    };

    private static PageBuilderController.SaveFilesModel HeaderGroup(params string[] types) => new()
    {
        Files = JsonConvert.SerializeObject(new[] { new { path = Path, type = "themes", content = Group(types) } }),
    };

    private static PageBuilderController.SaveFilesModel ThemeFile(string path) => new()
    {
        Files = JsonConvert.SerializeObject(new[] { new { path, type = "themes", content = new { content = Array.Empty<object>() } } }),
    };


    private PageBuilderController Builder(FakeAuthorizationService authorization = null, IEventPublisher eventPublisher = null, IContentHistory history = null)
    {
        var options = Options.Create(new GitContentOptions { Enabled = true, Organization = "acme", Token = "t0ken", BaseBranch = "main" });
        var themePublisher = new ThemePublisher(_themeStorageFactory, new ThemeSnapshotBuilder(), _live.StoreService, new SchemaCatalogService(_themeStorageFactory),
            _lock, _live, new NoContentHistory(), Options.Create(new ThemeSnapshotOptions()), NullLogger<ThemePublisher>.Instance);
        var controller = new PageBuilderController(
            storeService: _live.StoreService,
            pathResolver: _live,
            blobContentStorageProviderFactory: _live,
            publishingService: _live,
            eventPublisher: eventPublisher ?? _live,
            gitContentOptions: options,
            gitHubConnectionService: GitHubConnections.CreateService(options.Value),
            gitContentPolicy: GitHubConnections.CreatePolicy(options, new FakeSettingsManager(true), _ownership),
            gitContentRepository: null,
            gitContentHistory: null,
            gitContentPublisher: null,
            gitContentProvisioner: new FakeGitContentProvisioner(),
            gitLivePageService: null,
            settingsManager: null,
            authorizationService: authorization ?? new FakeAuthorizationService(true),
            distributedLock: _lock,
            requestValidator: RequestValidators.Content("s1"),
            contentHistory: history ?? new NoContentHistory(),
            themePublisher: themePublisher,
            themeSnapshotService: new ThemeSnapshotService(_live.StoreService, _settings, _themeStorageFactory, themePublisher, null,
                Options.Create(new ThemeSnapshotOptions())),
            themeDraftService: null,
            groupedPageService: null,
            pageContentService: null);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test")) },
        };
        return controller;
    }

    private sealed class GatedEventPublisher : IEventPublisher
    {
        public TaskCompletionSource Handling { get; } = new();

        public TaskCompletionSource Handled { get; } = new();

        public async Task Publish<T>(T @event, CancellationToken cancellationToken = default) where T : IEvent
        {
            Handling.TrySetResult();
            await Handled.Task;
        }
    }
}
