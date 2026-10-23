using System;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
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
    public static TheoryData<string> ValidPermalinks => LoadCases("valid");

    public static TheoryData<string> InvalidPermalinks => LoadCases("invalid");

    [Theory]
    [MemberData(nameof(ValidPermalinks))]
    public void IsValid_AllowedPermalink_ReturnsTrue(string permalink)
    {
        Assert.True(PageBuilderPermalink.IsValid(permalink));
    }

    [Theory]
    [MemberData(nameof(InvalidPermalinks))]
    public void IsValid_ForbiddenPermalink_ReturnsFalse(string permalink)
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

    // VCST-6513: a permalink longer than the column used to fail with 500 on the database write.
    [Fact]
    public async Task CreateGroupPage_PermalinkLongerThanTheColumn_ReturnsBadRequest()
    {
        var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        var controller = CreateController(service);

        var result = await controller.CreateGroupPage(new CreateGroupedPageRequest
        {
            StoreId = StoreId,
            Name = "Test page",
            Permalink = "/" + new string('a', PageBuilderPermalink.MaxLength),
            Content = EmptyContent,
        }, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
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

    [Fact]
    public async Task UpdateGroup_NewGroupWithInvalidPermalink_ReturnsBadRequestAndDoesNotSave()
    {
        var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        var controller = CreateController(service);

        var result = await controller.UpdateGroup(new GroupedPageBuilderPage
        {
            Id = GroupId,
            StoreId = StoreId,
            Name = "Test page",
            Permalink = "/test page",
        }, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(await service.GetByIdAsync(GroupId));
    }

    // Cases shared with the page builder shell tests (tests/TestData/permalink-cases.json).
    private static TheoryData<string> LoadCases(string kind)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "permalink-cases.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var cases = new TheoryData<string>();
        foreach (var item in document.RootElement.GetProperty(kind).EnumerateArray())
        {
            cases.Add(item.GetString());
        }
        return cases;
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
