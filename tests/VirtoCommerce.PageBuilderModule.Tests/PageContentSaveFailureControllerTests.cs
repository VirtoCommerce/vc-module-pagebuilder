using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;
using Fakes = VirtoCommerce.PageBuilderModule.Tests.PublishedRenameContentPreservationTests;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageContentSaveFailureControllerTests
{
    private const string GroupId = "group";
    private const string Original = """{"settings":{},"content":[]}""";
    private const string Restored = """{"settings":{"name":"Restored"},"content":[]}""";
    private const string VersionId = "HA_v-Hs8f7RVPw9gHHqUAxmPDJ8";

    private readonly Fakes.FakeGroupedPageService _pages = CreatePages();
    private readonly RecordingContentHistory _history = new();

    public PageContentSaveFailureControllerTests()
    {
        _history.Versions[(VersionId, $"page:{GroupId}")] = Restored;
    }

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
        Assert.Empty(_history.Records);
    }

    [Fact]
    public async Task Restore_PageGone_ReturnsNotFound()
    {
        _pages.SaveContentException = new KeyNotFoundException();

        Assert.IsType<NotFoundResult>(await RestoreAsync());
        Assert.Empty(_history.Records);
    }

    [Fact]
    public async Task Restore_ContentNamesWhatIsNotThere_ReturnsBadRequestWithTheReason()
    {
        _pages.SaveContentException = new InvalidDataException("Shared Component 'x' was not found.");

        var result = Assert.IsType<BadRequestObjectResult>(await RestoreAsync());

        Assert.Equal("""{"error":"Shared Component 'x' was not found."}""", PlatformJson.SerializeBody(result));
        Assert.Empty(_history.Records);
    }

    [Fact]
    public async Task Restore_AnyOtherFailure_IsLeftToTheServer()
    {
        _pages.SaveContentException = new InvalidOperationException();

        await Assert.ThrowsAsync<InvalidOperationException>(RestoreAsync);
        Assert.Empty(_history.Records);
    }

    [Fact]
    public async Task Restore_Saved_WritesTheDraftAndRecordsTheRestore()
    {
        var result = await RestoreAsync();

        var group = await _pages.GetByIdAsync(GroupId);
        Assert.Equal(Restored, (await _pages.LoadGroupContentAsync(group, cancellationToken: TestContext.Current.CancellationToken)).Content);
        var (_, record) = Assert.Single(_history.Records);
        Assert.Equal($$"""{"saveId":{{JsonConvert.ToString(record.SaveId)}}}""", PlatformJson.SerializeBody(result));
        Assert.Equal("Restored HA_v-Hs", record.Description);
        Assert.Equal([(new HistoryItem(HistoryItemType.Page, GroupId), Restored)], record.Entries.Select(x => (x.Item, x.Content)));
    }

    [Fact]
    public async Task SavePageContentJson_Saved_RecordsThePageAsOneVersion()
    {
        Assert.IsType<NoContentResult>(await SaveAsync());

        var (storeId, record) = Assert.Single(_history.Records);
        Assert.Equal("store", storeId);
        Assert.Equal("Saved", record.Description);
        Assert.Equal([(new HistoryItem(HistoryItemType.Page, GroupId), Restored)], record.Entries.Select(x => (x.Item, x.Content)));
    }

    [Fact]
    public async Task DiscardGroupDraft_NeverPublished_ReturnsBadRequestAndDeletesNothing()
    {
        _pages.SeedGroup(new GroupedPageBuilderPage { Id = "new", StoreId = "store", Pages = [new PageBuilderPage { Id = "d1", Status = Draft }] });

        Assert.IsType<BadRequestObjectResult>(await PageController().DiscardGroupDraft("new", TestContext.Current.CancellationToken));

        Assert.Single((await _pages.GetByIdAsync("new")).Pages);
        Assert.Empty(_history.Records);
    }

    [Fact]
    public async Task DiscardGroupDraft_WithADraft_DeletesItAndRecordsThePublishedContent()
    {
        _pages.SeedGroup(new GroupedPageBuilderPage
        {
            Id = GroupId,
            StoreId = "store",
            Pages = [new PageBuilderPage { Id = "published", Status = Published }, new PageBuilderPage { Id = "d1", Status = Draft }],
        });
        _pages.SeedContent("d1", Restored);

        Assert.IsType<OkResult>(await PageController().DiscardGroupDraft(GroupId, TestContext.Current.CancellationToken));

        Assert.Equal(["published"], (await _pages.GetByIdAsync(GroupId)).Pages.Select(x => x.Id));
        var (_, record) = Assert.Single(_history.Records);
        Assert.Equal("Discarded unpublished changes", record.Description);
        Assert.Equal([(new HistoryItem(HistoryItemType.Page, GroupId), Original)], record.Entries.Select(x => (x.Item, x.Content)));
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
        ((IConvertToActionResult)await HistoryController().Restore(VersionId, "store", [$"page:{GroupId}"], TestContext.Current.CancellationToken)).Convert();

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
            logger: NullLogger<PageBuilderPageController>.Instance,
            contentHistory: _history));

    private HistoryController HistoryController()
    {
        var live = new FakeBlobStorage();
        var drafts = new ThemeDraftService(new ThemeStorageFactory(live.StoreService, live, live, live), new InProcessDistributedLock(), live,
            NullLogger<ThemeDraftService>.Instance);
        return WithUser(new HistoryController(_history, _pages, ContentService(), drafts, new Fakes.AllowAllAuthorizationService(),
            RequestValidators.Stores("store"), RequestValidators.History("store")));
    }

    private static T WithUser<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test")) },
        };
        return controller;
    }
}
