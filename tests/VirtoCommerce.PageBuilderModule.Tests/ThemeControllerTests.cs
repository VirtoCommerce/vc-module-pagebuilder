using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;
using Permissions = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class ThemeControllerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly RecordingThemeServices _services = new();

    [Fact]
    public async Task ThemeActions_StoreUserDoesNotWorkIn_ReturnForbidWithoutCallingServices()
    {
        var controller = Controller(new FakeAuthorizationService("s2", Permissions.Read, Permissions.Update, Permissions.Publish, Permissions.Theme));

        Assert.IsType<ForbidResult>((await controller.Publish("s1", "abcdefgh", Ct)).Result);
        Assert.IsType<ForbidResult>((await controller.GetStatus("s1", Ct)).Result);
        Assert.IsType<ForbidResult>((await controller.SaveCatalog("s1", Ct)).Result);
        Assert.IsType<ForbidResult>(await controller.GetCatalog("s1", null, Ct));
        // Authorization comes before the body is looked at, so a null body is still forbidden.
        Assert.IsType<ForbidResult>((await controller.GeneratePalette("s1", null, Ct)).Result);
        Assert.IsType<ForbidResult>((await controller.CheckPalette("s1", null, Ct)).Result);

        Assert.Empty(_services.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("gone")]
    public async Task ThemeActions_UnknownStore_ReturnBadRequestWithoutCallingServices(string storeId)
    {
        var controller = Controller(new FakeAuthorizationService(true));

        Assert.IsType<BadRequestObjectResult>((await controller.Publish(storeId, "abcdefgh", Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetStatus(storeId, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.SaveCatalog(storeId, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>(await controller.GetCatalog(storeId, null, Ct));
        Assert.IsType<BadRequestObjectResult>((await controller.GeneratePalette(storeId, GenerationRequest("#1b789b"), Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.CheckPalette(storeId, CheckRequest(), Ct)).Result);

        Assert.Empty(_services.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("orange")]
    [InlineData("#12345")]
    public async Task GeneratePalette_NotAColor_ReturnsBadRequestWithoutGenerating(string primary)
    {
        var controller = Controller(new FakeAuthorizationService(true));

        Assert.IsType<BadRequestObjectResult>((await controller.GeneratePalette("s1", GenerationRequest(primary), Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GeneratePalette("s1", GenerationRequest("#1b789b", "teal"), Ct)).Result);

        Assert.Empty(_services.Calls);
    }

    [Fact]
    public async Task PaletteActions_NoBodyOrNoTokens_ReturnBadRequestWithoutCallingTheService()
    {
        var controller = Controller(new FakeAuthorizationService(true));
        var noTokens = GenerationRequest("#1b789b");
        noTokens.Tokens.Light = [];
        var generationWithoutTokens = GenerationRequest("#1b789b");
        generationWithoutTokens.Tokens = null;
        var checkWithoutTokens = CheckRequest();
        checkWithoutTokens.Tokens = null;
        var badToken = CheckRequest();
        badToken.Tokens.Dark = ["color primary"];

        Assert.IsType<BadRequestObjectResult>((await controller.GeneratePalette("s1", null, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GeneratePalette("s1", noTokens, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GeneratePalette("s1", generationWithoutTokens, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.CheckPalette("s1", null, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.CheckPalette("s1", new ThemePaletteCheckRequest { Tokens = new ThemePaletteTokens() }, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.CheckPalette("s1", checkWithoutTokens, Ct)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.CheckPalette("s1", badToken, Ct)).Result);

        Assert.Empty(_services.Calls);
    }

    [Fact]
    public async Task PaletteActions_StoreUserWorksIn_CallTheService()
    {
        var controller = Controller(new FakeAuthorizationService(true));

        Assert.IsType<OkObjectResult>((await controller.GeneratePalette("s1", GenerationRequest("#1b789b", "#f99e24"), Ct)).Result);
        Assert.IsType<OkObjectResult>((await controller.CheckPalette("s1", CheckRequest(), Ct)).Result);

        Assert.Equal(["generate #1b789b", "check"], _services.Calls);
    }

    [Fact]
    public async Task CheckPalette_Checked_WritesTheFailuresJson()
    {
        var controller = Controller(new FakeAuthorizationService(true));

        Assert.Equal(
            """{"failures":[{"scheme":"light","pair":"link","text":"color_accent_600","background":"color_neutral_50","textColor":"#cccccc","backgroundColor":"#ffffff","ratio":1.6,"minimumRatio":4.5}],"unchecked":2}""",
            PlatformJson.SerializeBody(await controller.CheckPalette("s1", CheckRequest(), Ct)));
    }

    private static ThemePaletteGenerationRequest GenerationRequest(string primary, string secondary = null) =>
        new() { Primary = primary, Secondary = secondary, Tokens = new ThemePaletteTokens { Light = ["color_primary_500"], Dark = [] } };

    private static ThemePaletteCheckRequest CheckRequest() =>
        new()
        {
            Palette = new ThemePalette { Light = new Dictionary<string, string> { ["color_primary_500"] = "#1b789b" } },
            Tokens = new ThemePaletteTokens { Light = ["color_primary_500"], Dark = [] },
        };

    [Fact]
    public async Task SaveCatalog_StoreUserWorksIn_SavesCatalog()
    {
        var controller = Controller(new FakeAuthorizationService(true), """{ "hash": "abcdefgh" }""");

        Assert.IsType<OkObjectResult>((await controller.SaveCatalog("s1", Ct)).Result);

        Assert.Equal(["save s1 { \"hash\": \"abcdefgh\" }"], _services.Calls);
    }

    [Fact]
    public async Task Publish_Published_WritesThePublicationJson()
    {
        var controller = Controller(new FakeAuthorizationService(true));

        Assert.Equal("""{"publication":1}""", PlatformJson.SerializeBody(await controller.Publish("s1", "abcdefgh", Ct)));
    }

    [Fact]
    public async Task GeneratePalette_BodyThatDidNotBind_ReturnsBadRequest()
    {
        var controller = Controller(new FakeAuthorizationService(true));
        controller.ModelState.AddModelError("request", "The JSON value could not be converted.");

        Assert.IsType<BadRequestObjectResult>((await controller.GeneratePalette("s1", null, Ct)).Result);
        Assert.Empty(_services.Calls);
    }

    [Fact]
    public async Task GetStatus_StoreUserWorksIn_WritesThePublicationAndDraftsJson()
    {
        var controller = Controller(new FakeAuthorizationService(true));

        Assert.Equal("""{"publication":3,"drafts":["sections/header-group.json"],"newDrafts":[]}""", PlatformJson.SerializeBody(await controller.GetStatus("s1", Ct)));
        Assert.Equal(["status s1"], _services.Calls);
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
        return new ThemeController(
            _services,
            _services,
            _services,
            authorization,
            RequestValidators.Stores("s1"),
            new ThemePaletteGenerationRequestValidator(new ThemePaletteTokensValidator()),
            new ThemePaletteCheckRequestValidator(new ThemePaletteTokensValidator()))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private sealed class RecordingThemeServices : IThemePublisher, ISchemaCatalogService, IThemePaletteService
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

        public Task<ThemeStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"status {storeId}");
            return Task.FromResult(new ThemeStatus { Publication = 3, Drafts = ["sections/header-group.json"] });
        }

        public Task ValidateFilesAsync(string storeId, string catalogHash, IDictionary<string, JToken> files, CancellationToken cancellationToken = default)
        {
            Calls.Add($"validate {storeId}");
            return Task.CompletedTask;
        }

        public Task<string> SaveAsync(string storeId, string catalogJson, CancellationToken cancellationToken = default)
        {
            Calls.Add($"save {storeId} {catalogJson}");
            return Task.FromResult("abcdefgh");
        }

        public ThemePaletteGenerationResult GeneratePalette(ThemePaletteGenerationRequest request)
        {
            Calls.Add($"generate {request.Primary}");
            return new ThemePaletteGenerationResult();
        }

        public ThemePaletteCheckResult CheckPalette(ThemePaletteCheckRequest request)
        {
            Calls.Add("check");
            return new ThemePaletteCheckResult
            {
                Failures =
                [
                    new PaletteContrastFailure
                    {
                        Scheme = "light",
                        Pair = "link",
                        Text = "color_accent_600",
                        Background = "color_neutral_50",
                        TextColor = "#cccccc",
                        BackgroundColor = "#ffffff",
                        Ratio = 1.6,
                        MinimumRatio = 4.5,
                    },
                ],
                Unchecked = 2,
            };
        }

        public Task<string> GetAsync(string storeId, string hash, CancellationToken cancellationToken = default)
        {
            Calls.Add($"get {storeId}");
            return Task.FromResult<string>(null);
        }
    }
}
