using System;
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
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class ThemeWriteLockTests
{
    private const string Path = "default/sections/header-group.json";
    private const string Sha = "1c0ffee1c0ffee1c0ffee1c0ffee1c0ffee1c0ff";
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
        Assert.False(_live.Files.ContainsKey($"{Path}-draft"));
        await publication.DisposeAsync();
        await save;
        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
    }

    [Fact]
    public async Task SaveTemplates_PageWhileThemePublicationRuns_DoesNotWait()
    {
        await using var publication = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);

        await Builder().SaveTemplates("s1", "default", Files("pages", "home.page"), draft: true).WaitAsync(Patience, Ct);

        Assert.True(_live.Files.ContainsKey("home.page-draft"));
    }

    [Fact]
    public async Task DiscardDraft_ThemePublicationRunning_WaitsForStoreLock()
    {
        // a publication reads a draft in two steps; a delete between them must not fail it
        _live.Files[Path] = "{}";
        _live.Files[$"{Path}-draft"] = "{}";
        var publication = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);

        var discard = Builder().DiscardDraft("s1", Path, "themes", "default");

        Assert.Equal(ThemeStorage.GetLockResource("s1"), await _lock.Blocked.Task.WaitAsync(Patience, Ct));
        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
        await publication.DisposeAsync();
        Assert.IsType<NoContentResult>(await discard);
        Assert.False(_live.Files.ContainsKey($"{Path}-draft"));
    }

    [Fact]
    public async Task Restore_ThemePublicationRunning_WaitsForStoreLock()
    {
        const string repoPath = "theme/default/sections/header-group.json";
        var github = new GitHubScript(
            ("GET", $"repos/acme/site-s1/contents/{repoPath}?ref={Sha}", (_, _) => GitHubScript.Json(Contents("{\"content\":[]}"))),
            ("GET", $"repos/acme/site-s1/contents/{repoPath}?ref=main", (_, _) => GitHubScript.Json(Contents("{}"))),
            ("PUT", $"repos/acme/site-s1/contents/{repoPath}", (_, _) => GitHubScript.Json("""{ "commit": { "sha": "c9" } }""", HttpStatusCode.Created)));
        var publication = await _lock.AcquireAsync(ThemeStorage.GetLockResource("s1"), cancellationToken: Ct);

        var restore = HistoryController(github).Restore(Sha, "s1", Path, cancellationToken: Ct);

        Assert.Equal(ThemeStorage.GetLockResource("s1"), await _lock.Blocked.Task.WaitAsync(Patience, Ct));
        Assert.False(_live.Files.ContainsKey($"{Path}-draft"));
        await publication.DisposeAsync();
        Assert.IsType<OkResult>(await restore);
        Assert.Equal("{\"content\":[]}", _live.Files[$"{Path}-draft"]);
        github.AssertDone();
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private static PageBuilderController.SaveFilesModel Files(string type, string path) =>
        new() { Files = JsonConvert.SerializeObject(new[] { new { path, type, content = new { settings = new { type = "settings" }, content = Array.Empty<object>() } } }) };

    private PageBuilderController Builder()
    {
        var options = Options.Create(new GitContentOptions());
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
            gitContentProvisioner: new FakeGitContentProvisioner(),
            gitLivePageService: null,
            settingsManager: null,
            authorizationService: new FakeAuthorizationService(true),
            distributedLock: _lock,
            requestValidator: RequestValidators.Content("s1"),
            themeFileHistoryService: new NoFileHistory()));
    }

    private ThemeFileHistoryController HistoryController(GitHubScript github)
    {
        var options = Options.Create(new GitContentOptions
        {
            Enabled = true,
            Organization = "acme",
            Token = "t0ken",
            BaseBranch = "main",
            GraphQlUrl = new Uri("https://api.github.test/graphql"),
        });
        var repository = new GitHubContentRepository(github, new MemoryCache(new MemoryCacheOptions()), options);
        var history = new ThemeFileHistoryService(GitHubConnections.CreatePolicy(options, new FakeSettingsManager(true), new FakeGitContentOwnership()), repository,
            new GitHubApiClient(github), GitHubConnections.CreateService(options.Value), new ThemeStorageFactory(_live.StoreService, _live, _live, _live), _lock, _live,
            options, NullLogger<ThemeFileHistoryService>.Instance);
        return WithUser(new ThemeFileHistoryController(history, new FakeAuthorizationService(true), RequestValidators.ThemeFiles("s1")));
    }

    private static T WithUser<T>(T controller) where T : ControllerBase
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        return controller;
    }

    private static string Contents(string content) =>
        new JObject { ["sha"] = "b0", ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) }.ToString();
}
