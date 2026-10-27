using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task Update_AnotherStore_IsBadRequestAndKeepsStore()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var controller = CreateController(fixture);

        root.StoreId = "another-store";
        root.Name = "Renamed";
        var moved = await controller.Update(root);
        root.StoreId = null;
        var withoutStore = await controller.Update(root);

        Assert.IsType<BadRequestObjectResult>(moved.Result);
        Assert.IsType<OkObjectResult>(withoutStore.Result);
        var saved = await fixture.Roots.GetByIdAsync(root.Id);
        Assert.Equal((StoreId, "Renamed"), (saved.StoreId, saved.Name));
    }

    [Fact]
    public async Task Create_DefaultCulture_MustBeAStoreLanguageAndIsStoredAsTheStoreSpellsIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = CreateController(fixture);

        var unknown = await controller.Create(new MultilanguagePage { StoreId = StoreId, Name = "FAQ", Permalink = "/faq", DefaultCultureName = "xx-XX" });
        var otherCase = await controller.Create(new MultilanguagePage { StoreId = StoreId, Name = "FAQ", Permalink = "/faq", DefaultCultureName = "NL-nl" });

        Assert.IsType<BadRequestObjectResult>(unknown.Result);
        var created = Assert.IsType<MultilanguagePage>(Assert.IsType<OkObjectResult>(otherCase.Result).Value);
        Assert.Equal("nl-NL", (await fixture.Roots.GetByIdAsync(created.Id)).DefaultCultureName);
    }

    [Fact]
    public async Task CreateOrUpdate_TooLongNameOrInvalidPermalink_IsBadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var controller = CreateController(fixture);
        var longName = new string('a', 1025);
        var longPermalink = "/" + new string('a', 2048);

        var results = new[]
        {
            (await controller.Create(new MultilanguagePage { StoreId = StoreId, Name = longName, Permalink = "/faq" })).Result,
            (await controller.Create(new MultilanguagePage { StoreId = StoreId, Name = "FAQ", Permalink = longPermalink })).Result,
            (await controller.Create(new MultilanguagePage { StoreId = StoreId, Name = "FAQ", Permalink = "/100%-off" })).Result,
            (await controller.Update(new MultilanguagePage { Id = root.Id, Name = longName, Permalink = root.Permalink })).Result,
            (await controller.Update(new MultilanguagePage { Id = root.Id, Name = root.Name, Permalink = "/about us" })).Result,
        };

        Assert.All(results, x => Assert.IsType<BadRequestObjectResult>(x));
        var saved = await fixture.Roots.GetByIdAsync(root.Id);
        Assert.Equal(("About us", "/about-us"), (saved.Name, saved.Permalink));
    }

    [Fact]
    public async Task Update_DefaultCultureWithoutVersion_IsBadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US");
        var controller = CreateController(fixture);

        root.DefaultCultureName = "nl-NL";
        var result = await controller.Update(root);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("en-US", (await fixture.Roots.GetByIdAsync(root.Id)).DefaultCultureName);
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
        var markWithoutPublishedSource = await controller.MarkUpToDate(version.Id, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(notAStoreLanguage.Result);
        Assert.IsType<BadRequestObjectResult>(convertVersion.Result);
        Assert.IsType<BadRequestObjectResult>(markLanding.Result);
        Assert.IsType<BadRequestObjectResult>(markWithoutPublishedSource.Result);
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
    public async Task Delete_RootWithLanguageVersions_IsBadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US");
        var controller = CreateController(fixture);

        var result = await controller.Delete(root.Id);

        // A Multilanguage Page with versions is archived (archive-all), like a Landing Page.
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(await fixture.Roots.GetByIdAsync(root.Id));
        Assert.Equal(root.Id, (await fixture.LoadGroupAsync(en.Id)).MultilanguagePageId);
    }

    [Fact]
    public async Task Delete_RootWithoutLanguageVersions_IsDeleted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var controller = CreateController(fixture);

        var result = await controller.Delete(root.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await fixture.Roots.GetByIdAsync(root.Id));
    }

    [Fact]
    public async Task UpdateGroup_BodyThatDoesNotBind_IsBadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = CreatePagesController(fixture);

        // MVC passes null when the body does not bind, e.g. userGroups sent as an array.
        var result = await controller.UpdateGroup(null!, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateGroup_IgnoresClientMultilanguageFields()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var controller = CreatePagesController(fixture);

        var result = await controller.CreateGroup(new GroupedPageBuilderPage
        {
            StoreId = StoreId,
            CultureName = "nl-NL",
            Name = "Over ons",
            Permalink = "/over-ons",
            MultilanguagePageId = root.Id,
            OverriddenFields = "Permalink",
            SourceRevisionDate = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);

        var created = Assert.IsType<GroupedPageBuilderPage>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var entity = await fixture.LoadGroupAsync(created.Id);
        Assert.Equal((null, null, "Over ons"), (entity.MultilanguagePageId, entity.SourceRevisionDate, entity.Name));
    }

    [Fact]
    public async Task MissingBody_IsBadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = CreateController(fixture);
        var pagesController = CreatePagesController(fixture);

        Assert.IsType<BadRequestObjectResult>((await controller.Search(null)).Result);
        Assert.IsType<BadRequestObjectResult>((await pagesController.SearchList(null, fixture.List, TestContext.Current.CancellationToken)).Result);
        Assert.IsType<BadRequestObjectResult>((await pagesController.FindPermalinkUsage(null, fixture.List, TestContext.Current.CancellationToken)).Result);
    }

    [Fact]
    public async Task Update_WithoutId_IsBadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = CreateController(fixture);

        var result = await controller.Update(new MultilanguagePage { Name = "FAQ", Permalink = "/faq" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task InvalidArgument_IsBadRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var controller = CreateController(fixture);

        var result = await controller.CreateLanguage(root.Id, " ", copyContent: false, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static PageBuilderPageController CreatePagesController(Fixture fixture)
    {
        return new PageBuilderPageController(
            null!,
            fixture.Groups,
            fixture.GroupSearch,
            new PredicateAuthorizationService(_ => true),
            null!,
            null!,
            NullLogger<PageBuilderPageController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
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
