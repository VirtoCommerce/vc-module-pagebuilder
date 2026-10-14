using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Tests.MultilanguagePageServiceTests;

namespace VirtoCommerce.PageBuilderModule.Tests;

[Collection(nameof(MultilanguagePageServiceTests))]
public class MultilanguagePageControllerTests
{
    private const string StoreId = "store";

    [Fact]
    public async Task Create_MissingFieldsOrUnknownStore_BadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = CreateController(fixture);

        var noName = await controller.Create(new MultilanguagePage { StoreId = StoreId, Permalink = "/faq" });
        var unknownStore = await controller.Create(new MultilanguagePage { StoreId = "unknown", Name = "FAQ", Permalink = "/faq" });

        Assert.IsType<BadRequestObjectResult>(noName.Result);
        Assert.IsType<BadRequestObjectResult>(unknownStore.Result);
    }

    [Fact]
    public async Task Create_IgnoresClientId()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = CreateController(fixture);

        var result = await controller.Create(new MultilanguagePage { Id = "client-id", StoreId = StoreId, Name = "FAQ", Permalink = "/faq" });

        var created = Assert.IsType<MultilanguagePage>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.NotEqual("client-id", created.Id);
        Assert.NotNull(await fixture.Roots.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task RootOfAnotherStore_IsForbidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var controller = CreateController(fixture, _ => false);

        Assert.Equal(StatusCodes.Status403Forbidden, StatusCode((await controller.Get(root.Id)).Result));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusCode((await controller.GetLanguages(root.Id, TestContext.Current.CancellationToken)).Result));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusCode(await controller.Delete(root.Id)));
        Assert.NotNull(await fixture.Roots.GetByIdAsync(root.Id));
    }

    [Fact]
    public async Task UnknownRoot_IsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = CreateController(fixture);

        Assert.IsType<NotFoundResult>((await controller.Get("missing")).Result);
        Assert.IsType<NotFoundResult>((await controller.PublishAll("missing", TestContext.Current.CancellationToken)).Result);
        Assert.IsType<NotFoundResult>((await controller.Unlink("missing", TestContext.Current.CancellationToken)).Result);
    }

    [Fact]
    public async Task Update_KeepsStore()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var controller = CreateController(fixture);

        root.StoreId = "another-store";
        root.Name = "Renamed";
        var result = await controller.Update(root);

        Assert.IsType<OkObjectResult>(result.Result);
        var saved = await fixture.Roots.GetByIdAsync(root.Id);
        Assert.Equal((StoreId, "Renamed"), (saved.StoreId, saved.Name));
    }

    [Fact]
    public async Task ServiceRejection_IsBadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var version = await fixture.CreateLanguageAsync(root, "nl-NL");
        var landing = await fixture.CreateLanguageAsync(null, "en-US", permalink: "/summer", name: "Summer");
        var controller = CreateController(fixture);

        var notAStoreLanguage = await controller.CreateLanguage(root.Id, "fr-FR", copyContent: false, TestContext.Current.CancellationToken);
        var convertVersion = await controller.Convert(version.Id, TestContext.Current.CancellationToken);
        var markLanding = await controller.MarkUpToDate(landing.Id, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(notAStoreLanguage.Result);
        Assert.IsType<BadRequestObjectResult>(convertVersion.Result);
        Assert.IsType<BadRequestObjectResult>(markLanding.Result);
    }

    [Fact]
    public async Task Link_RequiresAccessToThePage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var landing = await fixture.CreateLanguageAsync(null, "nl-NL", permalink: "/over-ons");
        var controller = CreateController(fixture, resource => resource is MultilanguagePage);

        var result = await controller.Link(root.Id, landing.Id, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status403Forbidden, StatusCode(result.Result));
        Assert.Null((await fixture.LoadGroupAsync(landing.Id)).MultilanguagePageId);
    }

    [Fact]
    public async Task Delete_KeepsLanguageVersionsAsLandingPages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US");
        var controller = CreateController(fixture);

        var result = await controller.Delete(root.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await fixture.Roots.GetByIdAsync(root.Id));
        Assert.Null((await fixture.LoadGroupAsync(en.Id)).MultilanguagePageId);
    }

    private static MultilanguagePageController CreateController(Fixture fixture, Func<object, bool> isAllowed = null)
    {
        return new MultilanguagePageController(
            fixture.Roots,
            null!,
            fixture.Groups,
            new PredicateAuthorizationService(isAllowed ?? (_ => true)),
            fixture.StoreService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static int? StatusCode(IActionResult result)
    {
        return (result as IStatusCodeActionResult)?.StatusCode;
    }

    private sealed class PredicateAuthorizationService(Func<object, bool> isAllowed) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(isAllowed(resource) ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
            Task.FromResult(AuthorizationResult.Success());
    }
}
