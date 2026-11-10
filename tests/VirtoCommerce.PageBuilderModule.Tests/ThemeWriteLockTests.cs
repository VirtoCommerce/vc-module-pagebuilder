using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class ThemeWriteLockTests
{
    private const string Path = "default/sections/header-group.json";
    private const string VersionId = "HA_v-Hs8f7RVPw9gHHqUAxmPDJ8";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly InProcessDistributedLock _lock = new();
    private readonly FakeBlobStorage _live = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveTemplates_ThemePublicationRunning_WaitsForStoreLock()
    {
        var publication = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);

        var save = Builder().SaveTemplates("s1", "default", Files("themes", Path), draft: true);

        Assert.Equal(ThemeStorage.GetLockResource("s1"), await _lock.Blocked.Task.WaitAsync(Patience, Ct));
        Assert.False(_live.Files.ContainsKey(Theme($"{Path}-draft")));
        await publication.DisposeAsync();
        await save;
        Assert.True(_live.Files.ContainsKey(Theme($"{Path}-draft")));
    }

    [Fact]
    public async Task SaveTemplates_PageWhileThemePublicationRuns_DoesNotWait()
    {
        await using var publication = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);

        await Builder().SaveTemplates("s1", "default", Files("pages", "home.page"), draft: true).WaitAsync(Patience, Ct);

        Assert.True(_live.Files.ContainsKey(Page("home.page-draft")));
    }

    [Fact]
    public async Task SaveDesignerChanges_PageOnlyWhileThemePublicationRuns_DoesNotWait()
    {
        await using var publication = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);
        var page = new DesignerSavePage { Path = "home.page", Type = "pages", Content = "{\"settings\":{\"type\":\"settings\"},\"content\":[]}" };

        await Builder().SaveDesignerChanges("s1", new DesignerSaveRequest { Page = page }).WaitAsync(Patience, Ct);

        Assert.True(_live.Files.ContainsKey(Page("home.page-draft")));
    }

    [Fact]
    public async Task DiscardDraft_ThemePublicationRunning_WaitsForStoreLock()
    {
        // A publication reads a draft in two steps, and a delete between them must not fail it.
        _live.Files[Theme(Path)] = "{}";
        _live.Files[Theme($"{Path}-draft")] = "{}";
        var publication = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);

        var discard = Builder().DiscardDraft("s1", Path, "themes", "default");

        Assert.Equal(ThemeStorage.GetLockResource("s1"), await _lock.Blocked.Task.WaitAsync(Patience, Ct));
        Assert.True(_live.Files.ContainsKey(Theme($"{Path}-draft")));
        await publication.DisposeAsync();
        Assert.IsType<NoContentResult>(await discard);
        Assert.False(_live.Files.ContainsKey(Theme($"{Path}-draft")));
    }

    [Fact]
    public async Task Restore_ThemePublicationRunning_WaitsForStoreLock()
    {
        var history = new RecordingContentHistory();
        history.Versions[(VersionId, $"theme:{Path}")] = "{\"content\":[]}";
        var publication = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);

        var restore = HistoryController(history).Restore(VersionId, "s1", [$"theme:{Path}"], Ct);

        Assert.Equal(ThemeStorage.GetLockResource("s1"), await _lock.Blocked.Task.WaitAsync(Patience, Ct));
        Assert.False(_live.Files.ContainsKey(Theme($"{Path}-draft")));
        await publication.DisposeAsync();
        Assert.IsType<OkObjectResult>((await restore).Result);
        Assert.Equal("{\"content\":[]}", _live.Files[Theme($"{Path}-draft")]);
        Assert.Single(history.Records);
    }

    [Fact]
    public async Task SaveDrafts_LockLostAndThePageFails_LeavesTheDraftsAsTheyAre()
    {
        _live.Files[Theme($"{Path}-draft")] = "{\"old\":true}";

        await Assert.ThrowsAnyAsync<Exception>(() => ThemeDrafts().SaveDraftsAsync<bool>("s1", new Dictionary<string, string> { [Path] = "{\"new\":true}" }, eTags: null, _ =>
        {
            _lock.LoseHeldLocks();
            throw new IOException("the page could not be saved");
        }, Ct));

        Assert.Equal("{\"new\":true}", _live.Files[Theme($"{Path}-draft")]);
    }

    [Fact]
    public async Task DiscardDrafts_LockLostAndThePageFails_LeavesTheDraftsAsTheyAre()
    {
        _live.Files[Theme(Path)] = "{}";
        _live.Files[Theme($"{Path}-draft")] = "{\"old\":true}";

        await Assert.ThrowsAnyAsync<Exception>(() => ThemeDrafts().DiscardDraftsAsync("s1", [Path], _ =>
        {
            _lock.LoseHeldLocks();
            throw new IOException("the page could not be discarded");
        }, Ct));

        Assert.False(_live.Files.ContainsKey(Theme($"{Path}-draft")));
    }

    private static string Theme(string path) => $"themes/s1/{path}";

    private static string Page(string path) => $"pages/s1/{path}";

    private static PageBuilderController.SaveFilesModel Files(string type, string path) =>
        new() { Files = JsonConvert.SerializeObject(new[] { new { path, type, content = new { settings = new { type = "settings" }, content = Array.Empty<object>() } } }) };

    private PageBuilderController Builder()
    {
        var options = Options.Create(new GitContentOptions());
        var filePageDrafts = TestServices.FilePageDrafts(_live, options, GitHubConnections.CreateService(options.Value), GitHubConnections.CreatePolicy(options, new FakeSettingsManager(false), new FakeGitContentOwnership()), null, null, new FakeGitContentProvisioner());
        return WithUser(new PageBuilderController(
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
            authorizationService: new FakeAuthorizationService(true),
            distributedLock: _lock,
            requestValidator: RequestValidators.Content("s1"),
            contentHistory: new NoContentHistory(),
            themeDraftService: ThemeDrafts(),
            groupedPageService: null,
            sharedComponentPlacementValidator: null,
            designerContentService: TestServices.Designer(new NoContentHistory(), filePageDrafts, ThemeDrafts(), null, _live, null, null),
            contentPermissionService: TestServices.Permissions(new FakeAuthorizationService(true)),
            filePageDraftService: filePageDrafts,
            designerSaveValidator: RequestValidators.DesignerSave,
            designerDiscardValidator: RequestValidators.DesignerDiscard,
            themeStorageFactory: new ThemeStorageFactory(_live.StoreService, _live, _live, _live)));
    }

    private HistoryController HistoryController(RecordingContentHistory history)
    {
        var authorization = new FakeAuthorizationService(true);
        return WithUser(new HistoryController(history, null, TestServices.Designer(history, themeDrafts: ThemeDrafts()), TestServices.Permissions(authorization), null,
            authorization, RequestValidators.Stores("s1"), RequestValidators.History("s1"), RequestValidators.Restore("s1")));
    }

    private ThemeDraftService ThemeDrafts() =>
        new(new ThemeStorageFactory(_live.StoreService, _live, _live, _live), _lock, _live, NullLogger<ThemeDraftService>.Instance);

    private static T WithUser<T>(T controller) where T : ControllerBase
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        return controller;
    }
}
