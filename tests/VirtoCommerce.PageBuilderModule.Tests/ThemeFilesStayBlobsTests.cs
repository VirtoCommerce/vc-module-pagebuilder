using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// Theme files are blobs on either flow: saving or discarding one asks GitHub nothing — not whose the store's
/// repository is, and not whether GitHub answers at all.
/// </summary>
public class ThemeFilesStayBlobsTests
{
    private const string Path = "default/sections/header-group.json";

    private readonly FakeLiveStorage _live = new();
    private readonly FakeGitContentOwnership _ownership = new("another-store");

    [Fact]
    public async Task A_theme_save_of_a_store_on_git_asks_GitHub_nothing()
    {
        Assert.IsType<OkObjectResult>(await Builder().SaveTemplates("s1", "default", HeaderGroup(), draft: true));

        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
        Assert.Empty(_ownership.Asked);
    }

    [Fact]
    public async Task Discarding_a_theme_draft_of_a_store_on_git_asks_GitHub_nothing()
    {
        _live.Files[Path] = "{}";
        _live.Files[$"{Path}-draft"] = "{}";

        Assert.IsType<NoContentResult>(await Builder().DiscardDraft("s1", Path, "themes", "default"));

        Assert.False(_live.Files.ContainsKey($"{Path}-draft"));
        Assert.Empty(_ownership.Asked);
    }

    [Fact]
    public async Task A_save_answers_once_its_content_changed_event_is_handled()
    {
        var publisher = new GatedPublisher();
        var save = Builder(eventPublisher: publisher).SaveTemplates("s1", "default", HeaderGroup(), draft: true);

        await publisher.Handling.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(save.IsCompleted);

        publisher.Handled.SetResult();
        Assert.IsType<OkObjectResult>(await save);
    }

    [Fact]
    public async Task A_draft_of_a_store_the_user_does_not_work_in_is_kept()
    {
        _live.Files[Path] = "{}";
        _live.Files[$"{Path}-draft"] = "{}";

        Assert.IsType<ForbidResult>(await Builder(new FakeAuthorizationService(false)).DiscardDraft("s1", Path, "themes", "default"));

        Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
    }

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
            gitContentPolicy: new GitContentPolicy(options, new FakeSettingsManager(true), _ownership),
            gitContentRepository: null,
            gitContentHistory: null,
            gitContentPublisher: null,
            gitContentProvisioner: new FakeGitContentProvisioner(),
            settingsManager: null,
            authorizationService: authorization ?? new FakeAuthorizationService(true),
            distributedLock: new InProcessDistributedLock());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test")) },
        };
        return controller;
    }

    /// <summary>Handles an event only when the test lets it.</summary>
    private sealed class GatedPublisher : IEventPublisher
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
