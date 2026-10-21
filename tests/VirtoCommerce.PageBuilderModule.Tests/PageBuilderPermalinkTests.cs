using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderPermalinkTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/test-page")]
    [InlineData("test-page")]
    [InlineData("/catalog/summer_sale.2026/")]
    [InlineData("/test%20page")]
    [InlineData("/страница")]
    [InlineData("/ページ")]
    public void IsValid_UrlPath_ReturnsTrue(string permalink)
    {
        Assert.True(PageBuilderPermalink.IsValid(permalink));
    }

    [Theory]
    [InlineData("/test page")]
    [InlineData(" /test-page")]
    [InlineData("/test-page ")]
    [InlineData("/test\tpage")]
    [InlineData("/test page")]
    [InlineData("/test\npage")]
    [InlineData("/test\u0001page")]
    [InlineData("/test&page")]
    [InlineData("/test?x")]
    [InlineData("/test#y")]
    [InlineData("/test&page?x#y")]
    [InlineData("/test\\page")]
    [InlineData("//test")]
    [InlineData("/test//page")]
    [InlineData("/./test")]
    [InlineData("/test/..")]
    public void IsValid_NotAUrlPath_ReturnsFalse(string permalink)
    {
        Assert.False(PageBuilderPermalink.IsValid(permalink));
    }

    [Theory]
    [InlineData("/test page")]
    [InlineData("/test&page?x#y")]
    public async Task CreateGroupPage_InvalidPermalink_ReturnsBadRequestAndDoesNotSave(string permalink)
    {
        var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        var controller = CreateController(service);

        var result = await controller.CreateGroupPage(new CreateGroupedPageRequest
        {
            StoreId = StoreId,
            Name = "Test page",
            Permalink = permalink,
            Content = EmptyContent,
        }, TestContext.Current.CancellationToken);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(PageBuilderPermalink.InvalidMessage, badRequest.Value);
        Assert.Empty(await service.GetAsync(["group-new-1"]));
    }

    [Fact]
    public async Task CreateGroup_InvalidPermalink_ReturnsBadRequestAndDoesNotSave()
    {
        var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        var controller = CreateController(service);

        var model = new GroupedPageBuilderPage
        {
            StoreId = StoreId,
            Name = "Test page",
            Permalink = "/test page",
        };

        var result = await controller.CreateGroup(model, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(model.Id);
    }

    [Fact]
    public async Task UpdateGroup_ChangedToInvalidPermalink_ReturnsBadRequestAndKeepsStoredPermalink()
    {
        var service = CreateServiceWithPublishedGroup("/valid");
        var controller = CreateController(service);

        var result = await controller.UpdateGroup(new GroupedPageBuilderPage
        {
            Id = GroupId,
            StoreId = StoreId,
            Name = "Original",
            Permalink = "/test&page?x#y",
        }, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("/valid", (await service.GetByIdAsync(GroupId)).Permalink);
    }

    [Fact]
    public async Task UpdateGroup_UnchangedLegacyInvalidPermalink_AllowsOtherChanges()
    {
        var service = CreateServiceWithPublishedGroup("/legacy page");
        var controller = CreateController(service);

        var result = await controller.UpdateGroup(new GroupedPageBuilderPage
        {
            Id = GroupId,
            StoreId = StoreId,
            Name = "Renamed",
            Permalink = "/legacy page",
        }, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Renamed", (await service.GetByIdAsync(GroupId)).Name);
    }

    private static PublishedRenameContentPreservationTests.FakeGroupedPageService CreateServiceWithPublishedGroup(string permalink)
    {
        var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        service.SeedGroup(new GroupedPageBuilderPage
        {
            Id = GroupId,
            StoreId = StoreId,
            Name = "Original",
            Permalink = permalink,
            Pages = [new PageBuilderPage { Id = "published", Status = Published }],
        });
        service.SeedContent("published", EmptyContent);
        return service;
    }

    private static PageBuilderPageController CreateController(PublishedRenameContentPreservationTests.FakeGroupedPageService service)
    {
        var pageService = new PublishedRenameContentPreservationTests.FakePageBuilderPageService(service);
        var pageContentService = new PageBuilderPageContentService(
            pageService,
            service,
            new NoopSharedComponentReferenceIndexService(),
            new PublishedRenameContentPreservationTests.NoopEventPublisher(),
            NullLogger<PageBuilderPageContentService>.Instance);
        var controller = new PageBuilderPageController(
            pageService,
            service,
            new PublishedRenameContentPreservationTests.FakeGroupedPageSearchService(),
            new PublishedRenameContentPreservationTests.AllowAllAuthorizationService(),
            new PublishedRenameContentPreservationTests.NoopPageDocumentSearchService(),
            pageContentService,
            NullLogger<PageBuilderPageController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
        };
        return controller;
    }

    private const string GroupId = "group";
    private const string StoreId = "store";
    private const string EmptyContent = "{ \"settings\": {}, \"content\": [] }";
}
