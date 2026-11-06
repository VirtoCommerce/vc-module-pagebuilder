using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;
using Fakes = VirtoCommerce.PageBuilderModule.Tests.PublishedRenameContentPreservationTests;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>A save of a page's content that does not go through, as a save and a restore from the history answer it.</summary>
public class PageContentSaveFailureControllerTests
{
    private const string GroupId = "group";
    private const string Original = """{"settings":{},"content":[]}""";
    private const string Restored = """{"settings":{"name":"Restored"},"content":[]}""";
    private const string Sha = "1c0ffee1c0ffee1c0ffee1c0ffee1c0ffee1c0ff";

    private readonly Fakes.FakeGroupedPageService _pages = CreatePages();
    private readonly RecordingHistory _history = new();

    [Fact]
    public async Task SavePageContentJson_SavedSinceRead_ReturnsPreconditionFailed()
    {
        var staleETag = PageBuilderContentVersion.Create(new GroupedPageBuilderPage { Id = "other" }, Original);

        var result = await PageController().SavePageContentJson(GroupId, new UpdatePageContentRequest { Content = Restored, ETag = staleETag },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status412PreconditionFailed, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task SavePageContentJson_PageGone_ReturnsNotFound()
    {
        _pages.SaveContentException = new KeyNotFoundException();

        Assert.IsType<NotFoundResult>(await SaveAsync());
    }

    [Fact]
    public async Task SavePageContentJson_ContentNamesWhatIsNotThere_ReturnsBadRequestWithTheReason()
    {
        _pages.SaveContentException = new InvalidDataException("Shared Component 'x' was not found.");

        var result = Assert.IsType<BadRequestObjectResult>(await SaveAsync());

        Assert.Equal("Shared Component 'x' was not found.", result.Value);
    }

    [Fact]
    public async Task SavePageContentJson_AnyOtherFailure_IsLeftToTheServer()
    {
        _pages.SaveContentException = new InvalidOperationException();

        await Assert.ThrowsAsync<InvalidOperationException>(SaveAsync);
    }

    [Fact]
    public async Task Restore_SavedSinceRead_ReturnsPreconditionFailed()
    {
        _pages.SaveContentException = new PageBuilderContentConflictException();

        var result = await RestoreAsync();

        Assert.Equal(StatusCodes.Status412PreconditionFailed, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(_history.Recorded);
    }

    [Fact]
    public async Task Restore_PageGone_ReturnsNotFound()
    {
        _pages.SaveContentException = new KeyNotFoundException();

        Assert.IsType<NotFoundResult>(await RestoreAsync());
        Assert.Empty(_history.Recorded);
    }

    [Fact]
    public async Task Restore_ContentNamesWhatIsNotThere_ReturnsBadRequestWithTheReason()
    {
        _pages.SaveContentException = new InvalidDataException("Shared Component 'x' was not found.");

        var result = Assert.IsType<BadRequestObjectResult>(await RestoreAsync());

        Assert.Equal("Shared Component 'x' was not found.", result.Value);
        Assert.Empty(_history.Recorded);
    }

    [Fact]
    public async Task Restore_AnyOtherFailure_IsLeftToTheServer()
    {
        _pages.SaveContentException = new InvalidOperationException();

        await Assert.ThrowsAsync<InvalidOperationException>(RestoreAsync);
        Assert.Empty(_history.Recorded);
    }

    [Fact]
    public async Task Restore_Saved_WritesTheETagJsonAndRecordsTheRestore()
    {
        var result = await RestoreAsync();

        var group = await _pages.GetByIdAsync(GroupId);
        Assert.Equal($$"""{"eTag":{{JsonConvert.ToString(PageBuilderContentVersion.Create(group, Restored))}}}""", PlatformJson.SerializeBody(result));
        Assert.Equal(["Restored 1c0ffee"], _history.Recorded);
    }

    private async Task<IActionResult> SaveAsync()
    {
        var controller = PageController();
        var group = await _pages.GetByIdAsync(GroupId);

        return await controller.SavePageContentJson(GroupId,
            new UpdatePageContentRequest { Content = Restored, ETag = PageBuilderContentVersion.Create(group, Original) },
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private async Task<IActionResult> RestoreAsync() =>
        await HistoryController().Restore(GroupId, Sha, cancellationToken: TestContext.Current.CancellationToken);

    private static Fakes.FakeGroupedPageService CreatePages()
    {
        var pages = new Fakes.FakeGroupedPageService();
        pages.SeedGroup(new GroupedPageBuilderPage
        {
            Id = GroupId,
            StoreId = "store",
            Pages = [new PageBuilderPage { Id = "published", Status = Published }],
        });
        pages.SeedContent("published", Original);
        return pages;
    }

    private PageBuilderPageContentService ContentService() =>
        new(new Fakes.FakePageBuilderPageService(_pages), _pages, new NoopSharedComponentReferenceIndexService(), new Fakes.NoopEventPublisher(),
            NullLogger<PageBuilderPageContentService>.Instance);

    private PageBuilderPageController PageController() =>
        WithUser(new PageBuilderPageController(
            crudService: new Fakes.FakePageBuilderPageService(_pages),
            groupedPageService: _pages,
            groupedPageSearchService: new Fakes.FakeGroupedPageSearchService(),
            authorizationService: new Fakes.AllowAllAuthorizationService(),
            pageDocumentSearchService: new Fakes.NoopPageDocumentSearchService(),
            pageContentService: ContentService(),
            logger: NullLogger<PageBuilderPageController>.Instance));

    private GroupedPageHistoryController HistoryController() =>
        WithUser(new GroupedPageHistoryController(_pages, _pages, new Fakes.FakePageBuilderPageService(_pages), ContentService(),
            new Fakes.AllowAllAuthorizationService(), _history));

    private static T WithUser<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test")) },
        };
        return controller;
    }

    /// <summary>A history with one change, the restored content, that keeps the summary of each change recorded.</summary>
    private sealed class RecordingHistory : IGroupedPageHistoryService
    {
        public List<string> Recorded { get; } = [];

        public Task RecordAsync(GroupedPageBuilderPage group, string content, string summary, ClaimsPrincipal user, string saveId,
            CancellationToken cancellationToken = default)
        {
            Recorded.Add(summary);
            return Task.CompletedTask;
        }

        public Task<string> ReadContentAsync(GroupedPageBuilderPage group, string sha, CancellationToken cancellationToken = default) =>
            Task.FromResult(Restored);

        public Task<GitFileHistoryResult> GetHistoryAsync(GroupedPageBuilderPage group, string after = null, int take = 20,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> GetPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
