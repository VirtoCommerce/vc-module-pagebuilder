using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>A theme publication and its catalogs are asked for only for a store that exists and that the user works in.</summary>
public class ThemeControllerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly RecordingThemeServices _services = new();

    [Fact]
    public async Task A_store_the_user_does_not_work_in_is_forbidden_before_anything_is_read_or_written()
    {
        var controller = Controller(new FakeAuthorizationService(false));

        Assert.IsType<ForbidResult>((await controller.Publish("s1", "abcdefgh", Ct)).Result);
        Assert.IsType<ForbidResult>((await controller.SaveCatalog("s1", Ct)).Result);
        Assert.IsType<ForbidResult>(await controller.GetCatalog("s1", null, Ct));

        Assert.Empty(_services.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("gone")]
    public async Task A_store_that_does_not_exist_is_refused_before_anything_is_read_or_written(string storeId)
    {
        var controller = Controller(new FakeAuthorizationService(true));

        Assert.IsType<BadRequestObjectResult>((await controller.Publish(storeId, "abcdefgh", Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.SaveCatalog(storeId, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>(await controller.GetCatalog(storeId, null, Ct));

        Assert.Empty(_services.Calls);
    }

    [Fact]
    public async Task A_store_the_user_works_in_gets_its_catalog_saved()
    {
        var controller = Controller(new FakeAuthorizationService(true), """{ "hash": "abcdefgh" }""");

        Assert.IsType<OkObjectResult>((await controller.SaveCatalog("s1", Ct)).Result);

        Assert.Equal(["save s1"], _services.Calls);
    }

    [Fact]
    public async Task Publish_Published_WritesThePublicationJson()
    {
        var controller = Controller(new FakeAuthorizationService(true));

        Assert.Equal("""{"publication":1}""", PlatformJson.SerializeBody(await controller.Publish("s1", "abcdefgh", Ct)));
    }

    [Fact]
    public async Task SaveCatalog_Saved_WritesTheHashJson()
    {
        var controller = Controller(new FakeAuthorizationService(true), """{ "hash": "abcdefgh" }""");

        Assert.Equal("""{"hash":"abcdefgh"}""", PlatformJson.SerializeBody(await controller.SaveCatalog("s1", Ct)));
    }

    private ThemeController Controller(FakeAuthorizationService authorization, string body = "")
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return new ThemeController(_services, _services, authorization, RequestValidators.Stores("s1"))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private sealed class RecordingThemeServices : IThemePublisher, ISchemaCatalogService
    {
        public List<string> Calls { get; } = [];

        public Task<int> PublishAsync(string storeId, string catalogHash, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            Calls.Add($"publish {storeId}");
            return Task.FromResult(1);
        }

        public Task<int> PublishFirstAsync(string storeId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"publish first {storeId}");
            return Task.FromResult(1);
        }

        public Task<string> SaveAsync(string storeId, string catalogJson, CancellationToken cancellationToken = default)
        {
            Calls.Add($"save {storeId}");
            return Task.FromResult("abcdefgh");
        }

        public Task<string> GetAsync(string storeId, string hash, CancellationToken cancellationToken = default)
        {
            Calls.Add($"get {storeId}");
            return Task.FromResult<string>(null);
        }
    }
}
