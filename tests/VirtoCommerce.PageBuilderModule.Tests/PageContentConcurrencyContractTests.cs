using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageContentConcurrencyContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpBindingPreservesVersionAndReturnsConflictToTheSecondClient(bool jsonBody)
    {
        // Exercise routing, query/header/body binding, and actual HTTP response serialization.
        // Authorization and persistence are covered separately; this host uses the existing service fake.
        var service = CreateService();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(ModuleConstants.Security.Permissions.Read, policy => policy.RequireAssertion(_ => true))
            .AddPolicy(ModuleConstants.Security.Permissions.Update, policy => policy.RequireAssertion(_ => true));
        builder.Services.AddControllers(options => options.Filters.Add(new AllowAnonymousFilter()))
            .AddApplicationPart(typeof(PageBuilderPageController).Assembly).AddControllersAsServices();
        builder.Services.AddTransient(_ => CreateController(service));
        await using var app = builder.Build();
        app.UseRouting();
        app.UseAuthorization();
        app.MapControllers().AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var authorA = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new System.Uri(app.Urls.Single()) };
        using var authorB = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = authorA.BaseAddress };
        const string path = "/api/page-builder-pages/grouped/group/content";
        using var readA = await authorA.GetAsync(path + "?draft=true&includeVersion=true", TestContext.Current.CancellationToken);
        using var readB = await authorB.GetAsync(path + "?draft=true", TestContext.Current.CancellationToken);
        Assert.True(readA.IsSuccessStatusCode, $"{readA.StatusCode}: {await readA.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        Assert.Equal("no-store", readA.Headers.CacheControl.ToString());
        using var envelope = JsonDocument.Parse(await readA.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var version = envelope.RootElement.GetProperty("eTag").GetString();
        Assert.Equal(readB.Headers.ETag.ToString(), version);
        Assert.Equal(Original, envelope.RootElement.GetProperty("content").GetString());

        using var missingVersion = await SendAsync(authorA, AuthorA, null);
        Assert.Equal((HttpStatusCode)StatusCodes.Status428PreconditionRequired, missingVersion.StatusCode);
        using var saved = await SendAsync(authorA, AuthorA, version);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.NotEqual(version, saved.Headers.ETag.ToString());
        using var conflict = await SendAsync(authorB, AuthorB, version);
        Assert.Equal(HttpStatusCode.PreconditionFailed, conflict.StatusCode);
        using var persisted = await authorB.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(AuthorA, await persisted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        async Task<HttpResponseMessage> SendAsync(HttpClient client, string content, string eTag)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path + (jsonBody ? "-json" : ""));
            request.Content = jsonBody
                ? JsonContent.Create(new UpdatePageContentRequest { Content = content, ETag = eTag })
                : new StringContent(content, Encoding.UTF8, "application/json");
            if (!jsonBody && eTag != null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", eTag);
            }
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task JsonToolCanReadAndSaveMatchingBodyVersionAndRejectStaleContent()
    {
        var controller = CreateController(CreateService());
        await controller.GetPageContent("group", cancellationToken: TestContext.Current.CancellationToken, includeVersion: true);
        controller.Response.Body.Position = 0;
        using var reader = new StreamReader(controller.Response.Body, leaveOpen: true);
        using var envelope = JsonDocument.Parse(await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Original, envelope.RootElement.GetProperty("content").GetString());
        var version = envelope.RootElement.GetProperty("eTag").GetString();
        Assert.Equal(controller.Response.Headers.ETag.ToString(), version);
        var request = new UpdatePageContentRequest { Content = AuthorA, ETag = version };
        Assert.IsType<NoContentResult>(await controller.SavePageContentJson("group", request, TestContext.Current.CancellationToken));
        request.Content = AuthorB;
        var stale = Assert.IsType<ObjectResult>(await controller.SavePageContentJson("group", request, TestContext.Current.CancellationToken));
        Assert.Equal(StatusCodes.Status412PreconditionFailed, stale.StatusCode);
    }

    [Fact]
    public async Task JsonToolCannotSendDifferentHeaderAndBodyVersions()
    {
        var controller = CreateController(CreateService());
        await controller.GetPageContent("group", true, TestContext.Current.CancellationToken);
        controller.Request.Headers.IfMatch = controller.Response.Headers.ETag;
        var result = await controller.SavePageContentJson("group",
            new UpdatePageContentRequest { Content = AuthorA, ETag = $"\"{new string('A', 64)}\"" },
            TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothSaveEndpointsRejectStaleSessionAndReturnNextVersion(bool jsonBody)
    {
        var service = CreateService();
        var first = CreateController(service);
        var second = CreateController(service);
        await first.GetPageContent("group", cancellationToken: TestContext.Current.CancellationToken);
        await second.GetPageContent("group", cancellationToken: TestContext.Current.CancellationToken);
        var version = first.Response.Headers.ETag.ToString();
        Assert.Equal(version, second.Response.Headers.ETag.ToString());
        Assert.NotEmpty(version);
        Assert.Equal("no-store", first.Response.Headers.CacheControl);

        first.Request.Headers.IfMatch = version;
        Assert.IsType<NoContentResult>(await SaveAsync(first, AuthorA, jsonBody));
        var nextVersion = first.Response.Headers.ETag.ToString();
        Assert.NotEqual(version, nextVersion);

        second.Request.Headers.IfMatch = version;
        var conflict = Assert.IsType<ObjectResult>(await SaveAsync(second, AuthorB, jsonBody));
        Assert.Equal(StatusCodes.Status412PreconditionFailed, conflict.StatusCode);
        Assert.Contains("content version does not match", Assert.IsType<string>(conflict.Value));
        var group = await service.GetByIdAsync("group");
        var draft = Assert.Single(group.Pages, x => x.Status == "Draft");
        Assert.Equal(AuthorA, await service.LoadContent(draft.Id, TestContext.Current.CancellationToken));

        first.Request.Headers.IfMatch = nextVersion;
        Assert.IsType<NoContentResult>(await SaveAsync(first, AuthorB, jsonBody));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingVersionCannotCreateDraft(bool jsonBody)
    {
        var service = CreateService();
        var controller = CreateController(service);
        var result = Assert.IsType<ObjectResult>(await SaveAsync(controller, AuthorA, jsonBody));
        Assert.Equal(StatusCodes.Status428PreconditionRequired, result.StatusCode);
        Assert.DoesNotContain((await service.GetByIdAsync("group")).Pages, x => x.Status == "Draft");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("W/\"version\"")]
    [InlineData("\"one\", \"two\"")]
    public async Task NonConcreteVersionCannotBypassProtection(string version)
    {
        var controller = CreateController(CreateService());
        controller.Request.Headers.IfMatch = version;
        Assert.IsType<BadRequestObjectResult>(await SaveAsync(controller, AuthorA, false));
    }

    [Fact]
    public async Task UnseededPageReturnsAnEmptyVersionThatCanBeUsedForFirstSave()
    {
        var service = CreateService();
        service.SeedGroup(new GroupedPageBuilderPage { Id = "group", StoreId = "store", Pages = [] });
        var controller = CreateController(service);
        await controller.GetPageContent("group", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        controller.Request.Headers.IfMatch = controller.Response.Headers.ETag;
        Assert.IsType<NoContentResult>(await SaveAsync(controller, AuthorA, false));
    }

    [Fact]
    public async Task VersionedUnseededPageCanBeSavedWithoutReadingAnotherVersion()
    {
        var service = CreateService();
        service.SeedGroup(new GroupedPageBuilderPage { Id = "group", StoreId = "store", Pages = [] });
        var controller = CreateController(service);
        await controller.GetPageContent("group", cancellationToken: TestContext.Current.CancellationToken, includeVersion: true);
        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
        controller.Response.Body.Position = 0;
        using var envelope = await JsonDocument.ParseAsync(controller.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        var request = new UpdatePageContentRequest
        {
            Content = AuthorA,
            ETag = envelope.RootElement.GetProperty("eTag").GetString(),
        };
        Assert.IsType<NoContentResult>(await controller.SavePageContentJson("group", request, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingGroupDoesNotReturnAnEditableEmptyVersion(bool includeVersion)
    {
        var controller = CreateController(CreateService());
        await controller.GetPageContent("missing", cancellationToken: TestContext.Current.CancellationToken, includeVersion: includeVersion);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        Assert.False(controller.Response.Headers.ContainsKey("ETag"));
        Assert.Equal(0, controller.Response.Body.Length);
    }

    [Fact]
    public void VersionDistinguishesContentAndAuthorizedGroupLifetime()
    {
        var group = new GroupedPageBuilderPage { Id = "group", StoreId = "store" };
        var version = PageBuilderContentVersion.Create(group, Original);
        Assert.NotEqual(PageBuilderContentVersion.Create(group, null), PageBuilderContentVersion.Create(group, ""));
        Assert.NotEqual(version, PageBuilderContentVersion.Create(group, AuthorA));
        group.Id = "other-group";
        Assert.NotEqual(version, PageBuilderContentVersion.Create(group, Original));
        group.Id = "group";
        group.StoreId = "other-store";
        Assert.NotEqual(version, PageBuilderContentVersion.Create(group, Original));
        group.StoreId = "store";
        group.CreatedDate = new System.DateTime(2026, 1, 1);
        Assert.NotEqual(version, PageBuilderContentVersion.Create(group, Original));
    }

    private static PublishedRenameContentPreservationTests.FakeGroupedPageService CreateService()
    {
        var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        service.SeedGroup(new GroupedPageBuilderPage
        {
            Id = "group",
            StoreId = "store",
            Pages = [new PageBuilderPage { Id = "published", Status = "Published" }],
        });
        service.SeedContent("published", Original);
        return service;
    }

    private static PageBuilderPageController CreateController(PublishedRenameContentPreservationTests.FakeGroupedPageService service)
    {
        var pageService = new PublishedRenameContentPreservationTests.FakePageBuilderPageService(service);
        var contentService = new PageBuilderPageContentService(pageService, service, new NoopSharedComponentReferenceIndexService(),
            new PublishedRenameContentPreservationTests.NoopEventPublisher(), NullLogger<PageBuilderPageContentService>.Instance);
        return new PageBuilderPageController(pageService, service,
            new PublishedRenameContentPreservationTests.FakeGroupedPageSearchService(),
            new PublishedRenameContentPreservationTests.AllowAllAuthorizationService(),
            new PublishedRenameContentPreservationTests.NoopPageDocumentSearchService(), contentService,
            NullLogger<PageBuilderPageController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } } },
        };
    }

    private static Task<IActionResult> SaveAsync(PageBuilderPageController controller, string content, bool jsonBody)
    {
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return jsonBody
            ? controller.SavePageContentJson("group", new UpdatePageContentRequest { Content = content }, TestContext.Current.CancellationToken)
            : controller.SavePageContent("group", TestContext.Current.CancellationToken);
    }

    private const string Original = "{\"settings\":{},\"content\":[]}";
    private const string AuthorA = "{\"settings\":{\"name\":\"Author A\"},\"content\":[]}";
    private const string AuthorB = "{\"settings\":{\"name\":\"Author B\"},\"content\":[]}";
}
