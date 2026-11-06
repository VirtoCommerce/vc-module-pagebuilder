using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Events;
using Xunit;
using Permissions = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderControllerThemeFilesTests
{
    private const string Path = "default/sections/header-group.json";

    private readonly FakeBlobStorage _live = new();
    private readonly FakeGitContentOwnership _ownership = new("another-store");

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

    private static PageBuilderController.SaveFilesModel ThemeFile(string path) => new()
    {
        Files = JsonConvert.SerializeObject(new[] { new { path, type = "themes", content = new { content = Array.Empty<object>() } } }),
    };

    private static PageBuilderController.SaveFilesModel HeaderGroup() => new()
    {
        Files = JsonConvert.SerializeObject(new[] { new { path = Path, type = "themes", content = new { content = Array.Empty<object>() } } }),
    };

    private PageBuilderController Builder(FakeAuthorizationService authorization = null, IEventPublisher eventPublisher = null)
    {
        var options = Options.Create(new GitContentOptions { Enabled = true, Organization = "acme", Token = "t0ken", BaseBranch = "main" });
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
            distributedLock: new InProcessDistributedLock(),
            requestValidator: RequestValidators.Content("s1"),
            themeFileHistoryService: new NoFileHistory());
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
