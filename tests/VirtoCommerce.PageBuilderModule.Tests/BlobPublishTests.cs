using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// The designer and the admin page blade publish through these actions, so the builder's permission and
/// store checks guard a blob publish.
/// </summary>
public class BlobPublishTests
{
    private const string Page = "about-us.page";

    [Fact]
    public async Task Publish_PublishesTheBlob()
    {
        var publishing = new RecordingPublishingService();

        var result = Assert.IsType<OkObjectResult>(await Controller(publishing).GitPublish("vccom", Page, "pages"));

        Assert.Equal(("pages", "vccom", Page, true), Assert.Single(publishing.Calls));
        Assert.Equal("Merged", JObject.FromObject(result.Value).Value<string>("state"));
    }

    [Fact]
    public async Task Unpublish_UnpublishesTheBlob()
    {
        var publishing = new RecordingPublishingService();

        Assert.IsType<OkResult>(await Controller(publishing).GitUnpublish("vccom", Page, "blogs"));

        Assert.Equal(("blogs", "vccom", Page, false), Assert.Single(publishing.Calls));
    }

    [Fact]
    public async Task PublishStatus_ReportsTheBlobStatus()
    {
        var publishing = new RecordingPublishingService { Status = new FilePublishStatus { Published = true, HasChanges = true } };

        var result = Assert.IsType<OkObjectResult>(await Controller(publishing).GitPublishStatus("vccom", Page, "pages"));

        var status = JObject.FromObject(result.Value);
        Assert.True(status.Value<bool>("published"));
        Assert.True(status.Value<bool>("hasChanges"));
        Assert.Equal("blob", status.Value<string>("flow"));
    }

    [Fact]
    public async Task Publish_ForAStoreTheUserCannotReach_PublishesNothing()
    {
        var publishing = new RecordingPublishingService();

        Assert.IsType<ForbidResult>(await Controller(publishing, allowed: false).GitPublish("vccom", Page, "pages"));

        Assert.Empty(publishing.Calls);
    }

    [Fact]
    public async Task Unpublish_ForAStoreTheUserCannotReach_UnpublishesNothing()
    {
        var publishing = new RecordingPublishingService();

        Assert.IsType<ForbidResult>(await Controller(publishing, allowed: false).GitUnpublish("vccom", Page, "pages"));

        Assert.Empty(publishing.Calls);
    }

    [Theory]
    [InlineData("../about-us.page")]
    [InlineData("about-us.json")]
    public async Task Publish_APathThatIsNotAPage_PublishesNothing(string path)
    {
        var publishing = new RecordingPublishingService();

        Assert.IsType<BadRequestObjectResult>(await Controller(publishing).GitPublish("vccom", path, "pages"));

        Assert.Empty(publishing.Calls);
    }

    private static PageBuilderController Controller(RecordingPublishingService publishing, bool allowed = true)
    {
        var controller = new PageBuilderController(
            storeService: new FakeStoreService(new Store { Id = "vccom" }),
            pathResolver: null,
            blobContentStorageProviderFactory: null,
            publishingService: publishing,
            eventPublisher: null,
            authorizationService: new FakeAuthorizationService(allowed),
            requestValidator: new PageBuilderContentRequestValidator(new FakeStoreService(new Store { Id = "vccom" }), new PageBuilderContentFileValidator()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "john")], "test")) },
        };

        return controller;
    }

    private sealed class RecordingPublishingService : IPublishingService
    {
        public List<(string ContentType, string StoreId, string RelativeUrl, bool Publish)> Calls { get; } = [];

        public FilePublishStatus Status { get; init; } = new();

        public Task PublishingAsync(string contentType, string storeId, string relativeUrl, bool publish)
        {
            Calls.Add((contentType, storeId, relativeUrl, publish));
            return Task.CompletedTask;
        }

        public Task<FilePublishStatus> PublishStatusAsync(string contentType, string storeId, string relativeUrl) =>
            Task.FromResult(Status);

        public Task<IEnumerable<ContentFile>> SetFilesStatuses(IEnumerable<ContentFile> files) => Task.FromResult(files);

        public string GetRelativeDraftUrl(string source, bool draft) => draft ? source + "-draft" : source;

        public bool IsDraft(string relativeUrl) => relativeUrl.EndsWith("-draft");
    }
}
