using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class RepositoryOwnershipConflictAttributeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void OnException_RepositoryOwnershipException_ReturnsConflict()
    {
        var context = Context(new GitRepositoryOwnershipException("acme/site-a", "a"));

        new RepositoryOwnershipConflictAttribute().OnException(context);

        Assert.True(context.ExceptionHandled);
        var conflict = Assert.IsType<ConflictObjectResult>(context.Result);
        Assert.Contains("belongs to store \"a\"", conflict.Value!.GetType().GetProperty("error")!.GetValue(conflict.Value) as string);
    }

    [Fact]
    public void OnException_OtherException_LeavesItUnhandled()
    {
        var context = Context(new InvalidOperationException("something else"));

        new RepositoryOwnershipConflictAttribute().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(typeof(PageBuilderController))]
    [InlineData(typeof(HistoryController))]
    [InlineData(typeof(GitContentSetupController))]
    public void Controller_UsesStoreRepository_HasConflictAttribute(Type controller)
    {
        Assert.NotNull(controller.GetCustomAttribute<RepositoryOwnershipConflictAttribute>());
    }

    [Theory]
    [InlineData("theme:default/templates/product.json")]
    [InlineData("page:g1")]
    public async Task GetHistory_ItemInAnotherStoresRepository_ThrowsOwnershipException(string item)
    {
        var live = new FakeBlobStorage();
        var pages = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        pages.SeedGroup(new GroupedPageBuilderPage { Id = "g1", StoreId = "s1", Name = "Home", Permalink = "/home" });
        var history = SiteHistories.Create(new GitHubScript(), new ThemeStorageFactory(live.StoreService, live, live, live), pages,
            ownership: new FakeGitContentOwnership("another-store"));
        var authorization = new FakeAuthorizationService(true);
        var controller = new HistoryController(history, pages, null, TestServices.Permissions(authorization), null, authorization, RequestValidators.Stores("s1"),
            RequestValidators.History("s1"), RequestValidators.Restore("s1"))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } },
        };

        await Assert.ThrowsAsync<GitRepositoryOwnershipException>(() => controller.GetHistory("s1", [item], cancellationToken: Ct));
    }

    [Fact]
    public async Task ConnectSite_RepositoryOfAnotherStore_ThrowsOwnershipException()
    {
        var provisioner = new FakeGitContentProvisioner { ConnectFailure = new GitRepositoryOwnershipException("acme/site-s1", "S1") };
        var connection = GitHubConnections.CreateService(new FakeSettingsManager());
        var controller = new GitContentSetupController(new GitContentSetupService(connection, provisioner, Policy(), new EphemeralDataProtectionProvider()),
            provisioner, connection, new FakeAuthorizationService(true), null, RequestValidators.Stores("s1"), RequestValidators.AppSetup)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        await Assert.ThrowsAsync<GitRepositoryOwnershipException>(() => controller.ConnectSite("s1", Ct));
    }

    private static ExceptionContext Context(Exception exception) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), []) { Exception = exception };

    private static IOptions<GitContentOptions> Options() => Microsoft.Extensions.Options.Options.Create(new GitContentOptions
    {
        Enabled = true,
        Organization = "acme",
        Token = "t0ken",
        BaseBranch = "main",
        GraphQlUrl = new Uri("https://api.github.test/graphql"),
    });

    private static GitContentPolicy Policy() => GitHubConnections.CreatePolicy(Options(), new FakeSettingsManager(true), new FakeGitContentOwnership("another-store"));
}
