using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;
using StorePermissions = VirtoCommerce.StoreModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GitContentSetupControllerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeSettingsManager _settings = new();
    private readonly EphemeralDataProtectionProvider _keys = new();

    [Fact]
    public async Task AppNew_ValidState_ReturnsGitHubFormWithSiteRepositoryManifest()
    {
        var controller = Controller();
        var state = await GetSetupStateAsync(controller, "acme", "B2B store");
        var (action, manifest) = await GetSetupFormAsync(controller, state);

        Assert.StartsWith("https://github.com/organizations/acme/settings/apps/new?state=", action);
        // the platform's CSP lets its pages post forms only to themselves
        Assert.Equal("https://github.com", controller.HttpContext.Items["Content-Security-Policy-Form-Action-Uri"]);

        Assert.Equal("Page Builder acme", manifest["name"]?.Value<string>());
        Assert.False(manifest["public"]?.Value<bool>());
        Assert.Equal("https://platform.test/api/pagebuilder/git/app/created", manifest["redirect_url"]?.Value<string>());
        Assert.Equal($"https://platform.test/api/pagebuilder/git/app/installed?state={Uri.EscapeDataString(state)}", manifest["setup_url"]?.Value<string>());
        Assert.Null(manifest["hook_attributes"]);
        Assert.Equal(
            JObject.Parse("""{ "administration": "write", "contents": "write", "pull_requests": "write", "metadata": "read" }"""),
            manifest["default_permissions"],
            JToken.EqualityComparer);
    }

    [Fact]
    public async Task AppNew_LongOrganizationName_KeepsAppNameWithinGitHubLimit()
    {
        var controller = Controller();
        var (_, manifest) = await GetSetupFormAsync(controller, await GetSetupStateAsync(controller, new string('a', 39), "s1"));

        Assert.True(manifest["name"]!.Value<string>()!.Length <= 34);
    }

    [Theory]
    [InlineData("")]
    [InlineData("acme/../settings")]
    [InlineData("-acme")]
    public async Task StartAppSetup_InvalidOrganization_ReturnsBadRequest(string organization)
    {
        Assert.IsType<BadRequestObjectResult>((await Controller().StartAppSetup(organization, "s1", Ct)).Result);
    }

    [Fact]
    public async Task AppCreated_ValidCode_SavesAppAndRedirectsToInstallation()
    {
        var github = GitHubConnections.CreateManifestConversion("acme");
        var connection = GitHubConnections.CreateService(_settings, _keys, github);
        var controller = Controller(connection);
        var state = await GetSetupStateAsync(controller, "acme", "s1");

        var result = await controller.AppCreated("c0de", state, Ct);

        github.AssertDone();
        Assert.Equal("https://github.com/apps/page-builder-acme/installations/new", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("77", (await connection.GetConnectionAsync(TestContext.Current.CancellationToken)).App?.AppId);
    }

    [Fact]
    public async Task AppCreated_ForgedState_ReturnsBadRequestWithoutCallingGitHub()
    {
        var github = GitHubConnections.CreateManifestConversion("acme");

        var result = await Controller(GitHubConnections.CreateService(_settings, _keys, github)).AppCreated("c0de", "forged", Ct);

        Assert.Equal(400, Assert.IsType<ContentResult>(result).StatusCode);
        Assert.Equal(400, Assert.IsType<ContentResult>(await Controller().AppNew("forged", Ct)).StatusCode);
        Assert.Equal(0, github.Requests);
        Assert.Empty(_settings.Values);
    }

    [Fact]
    public async Task AppInstalled_ValidState_RedirectsToSite()
    {
        var controller = Controller();
        var state = await GetSetupStateAsync(controller, "acme", "B2B store");

        Assert.Equal("/apps/admin-shell/#/content/site/B2B%20store", Assert.IsType<RedirectResult>(controller.AppInstalled(state)).Url);
        Assert.Equal("/apps/admin-shell/#/content/site/B2B%20store", Assert.IsType<RedirectResult>(controller.AppInstalled($"{state}?installation_id=9")).Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("B2B store")]
    [InlineData("forged")]
    public void AppInstalled_InvalidState_RedirectsToSitesPanel(string state)
    {
        Assert.Equal("/apps/admin-shell/#/content", Assert.IsType<RedirectResult>(Controller().AppInstalled(state)).Url);
    }

    [Fact]
    public async Task GetSite_SetupApp_ReturnsAppSourceAndInstallUrl()
    {
        var connection = GitHubConnections.CreateService(_settings, _keys, GitHubConnections.CreateManifestConversion("acme"));
        await connection.CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        var status = Assert.IsType<GitContentSiteStatus>(Assert.IsType<OkObjectResult>((await Controller(connection).GetSite("s1", Ct)).Result).Value);

        Assert.Equal(GitHubConnectionSource.App, status.Source);
        Assert.Equal("app", JObject.FromObject(status)[nameof(GitContentSiteStatus.Source)]?.Value<string>());
        Assert.Equal("https://github.com/apps/page-builder-acme/installations/new", status.InstallUrl);
    }

    [Fact]
    public async Task GetSite_StoreUserDoesNotWorkIn_ReturnsForbid()
    {
        var controller = Controller(authorization: new FakeAuthorizationService("s2"));

        Assert.IsType<ForbidResult>((await controller.GetSite("s1", Ct)).Result);
    }

    [Fact]
    public async Task GetConnection_SeveralStores_ListsOnlyStoresUserWorksIn()
    {
        var connection = GitHubConnections.CreateService(_settings, _keys, configure: options =>
        {
            options.Enabled = true;
            options.Organization = "acme";
            options.Token = "t0ken";
            options.BaseBranch = "main";
        });
        var controller = Controller(connection, new FakeAuthorizationService("s2"), new FakeGitContentPolicy(repositoryFormat: "acme/site-{0}"), new FakeStoreSearchService("s1", "s2"));

        var answer = PlatformJson.ParseBody(await controller.GetConnection(cancellationToken: Ct));

        Assert.Equal(["s2"], answer["sites"]!.Select(x => x.Value<string>("storeId")));
    }

    [Fact]
    public async Task GetConnection_StoreBeyondFirstPage_ListsIt()
    {
        var controller = Controller(ConfiguredConnection(), new FakeAuthorizationService("s150"), stores: new FakeStoreSearchService(Enumerable.Range(1, 250).Select(x => $"s{x:000}").ToArray()));

        var answer = PlatformJson.ParseBody(await controller.GetConnection(cancellationToken: Ct));

        Assert.Equal(["s150"], answer["sites"]!.Select(x => x.Value<string>("storeId")));
    }

    [Fact]
    public async Task GetConnection_GitHubTimesOut_ReturnsTimeoutError()
    {
        // what the GitHub client makes of an HttpClient.Timeout
        var timeout = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
        var provisioner = new FakeGitContentProvisioner { OrganizationFailure = new GitContentException($"GitHub request to read organization \"acme\" failed: {timeout.Message}", timeout) };
        var controller = Controller(ConfiguredConnection(), provisioner: provisioner);

        var answer = PlatformJson.ParseBody(await controller.GetConnection(cancellationToken: Ct));

        Assert.Contains("HttpClient.Timeout", answer.Value<string>("error"));
    }

    [Fact]
    public async Task ConnectSite_UnknownStore_ReturnsBadRequestAndConnectsNothing()
    {
        var provisioner = new FakeGitContentProvisioner();

        var result = await Controller(provisioner: provisioner).ConnectSite("gone", Ct);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(provisioner.Connected);
    }

    [Fact]
    public async Task ConnectSite_StoreUserDoesNotWorkIn_ReturnsForbidAndConnectsNothing()
    {
        var provisioner = new FakeGitContentProvisioner();

        var result = await Controller(authorization: new FakeAuthorizationService("s2", StorePermissions.Update), provisioner: provisioner).ConnectSite("s1", Ct);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(provisioner.Connected);
    }

    [Fact]
    public async Task ConnectAndDisconnectSite_NoStoreUpdateInTheStore_ReturnForbid()
    {
        var provisioner = new FakeGitContentProvisioner();
        var controller = Controller(authorization: new FakeAuthorizationService("s1"), provisioner: provisioner);

        Assert.IsType<ForbidResult>((await controller.ConnectSite("s1", Ct)).Result);
        Assert.IsType<ForbidResult>((await controller.DisconnectSite("s1", Ct)).Result);
        Assert.Empty(provisioner.Connected);
    }

    [Fact]
    public async Task ConnectSite_StoreUpdateInTheStore_Connects()
    {
        var provisioner = new FakeGitContentProvisioner();

        var result = await Controller(authorization: new FakeAuthorizationService("s1", StorePermissions.Update), provisioner: provisioner).ConnectSite("s1", Ct);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(["s1"], provisioner.Connected);
    }

    [Fact]
    public async Task StartAppSetup_ConfigurationInCharge_ReturnsBadRequest()
    {
        var connection = GitHubConnections.CreateService(_settings, _keys, configure: options =>
        {
            options.Organization = "env-org";
            options.Token = "t0ken";
            options.BaseBranch = "main";
        });

        Assert.IsType<BadRequestObjectResult>((await Controller(connection).StartAppSetup("acme", "s1", Ct)).Result);
    }

    [Fact]
    public async Task GetConnection_SetupApp_WritesTheSitesPanelJson()
    {
        var connection = GitHubConnections.CreateService(_settings, _keys, GitHubConnections.CreateManifestConversion("acme"));
        await connection.CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        var body = PlatformJson.SerializeBody(await Controller(connection, stores: new FakeStoreSearchService("s1", "s2")).GetConnection(cancellationToken: Ct));

        Assert.Equal(
            """
            {"sites":[{"storeId":"s1","name":"s1","repository":"acme/site-s1","url":"https://github.com/acme/site-s1","enabled":true},
            {"storeId":"s2","name":"s2","repository":"acme/site-s2","url":"https://github.com/acme/site-s2","enabled":true}],
            "configured":true,"source":"app","organization":"acme","organizationUrl":"https://github.com/acme","appSlug":"page-builder-acme",
            "appUrl":"https://github.com/organizations/acme/settings/apps/page-builder-acme",
            "installUrl":"https://github.com/apps/page-builder-acme/installations/new","autoCreateRepositories":true}
            """.ReplaceLineEndings(string.Empty),
            body);
    }

    [Fact]
    public async Task GetConnection_NotConfigured_WritesTheSetupJson()
    {
        Assert.Equal("""{"sites":[],"configured":false,"autoCreateRepositories":false}""", PlatformJson.SerializeBody(await Controller().GetConnection(cancellationToken: Ct)));
    }

    [Fact]
    public async Task StartAppSetup_ValidOrganization_WritesTheSetupPageUrlJson()
    {
        var body = PlatformJson.ParseBody(await Controller().StartAppSetup("acme", "s1", Ct));

        Assert.Equal(["url"], body.Properties().Select(x => x.Name));
        Assert.StartsWith("/api/pagebuilder/git/app/new?state=", body.Value<string>("url"));
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private GitHubConnectionService ConfiguredConnection() =>
        GitHubConnections.CreateService(_settings, _keys, configure: options =>
        {
            options.Enabled = true;
            options.Organization = "acme";
            options.Token = "t0ken";
            options.BaseBranch = "main";
        });

    private GitContentSetupController Controller(IGitHubConnectionService connection = null, IAuthorizationService authorization = null,
        IGitContentPolicy policy = null, IStoreSearchService stores = null, FakeGitContentProvisioner provisioner = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("platform.test");

        provisioner ??= new FakeGitContentProvisioner();
        connection ??= GitHubConnections.CreateService(_settings, _keys);
        var setup = new GitContentSetupService(connection, provisioner, policy ?? new FakeGitContentPolicy(repositoryFormat: "acme/site-{0}"), _keys);

        return new GitContentSetupController(setup, provisioner, connection, authorization ?? new FakeAuthorizationService(true), stores ?? new FakeStoreSearchService(),
            RequestValidators.Stores("s1", "s2"), RequestValidators.AppSetup)
        {
            ControllerContext = MvcRouting.CreateContext(http, "GitContentSetup"),
        };
    }

    private sealed class FakeStoreSearchService(params string[] ids) : IStoreSearchService
    {
        public Task<StoreSearchResult> SearchAsync(StoreSearchCriteria criteria, bool clone = true) =>
            Task.FromResult(new StoreSearchResult
            {
                Results = ids.Skip(criteria.Skip).Take(criteria.Take).Select(id => new Store { Id = id, Name = id }).ToList(),
                TotalCount = ids.Length,
            });
    }

    private static async Task<string> GetSetupStateAsync(GitContentSetupController controller, string organization, string storeId)
    {
        var url = Assert.IsType<GitHubAppSetupResult>(Assert.IsType<OkObjectResult>((await controller.StartAppSetup(organization, storeId, Ct)).Result).Value).Url;
        Assert.StartsWith("/api/pagebuilder/git/app/new?state=", url);
        return HttpUtility.ParseQueryString(url[url.IndexOf('?')..])["state"];
    }

    private static async Task<(string Action, JObject Manifest)> GetSetupFormAsync(GitContentSetupController controller, string state)
    {
        var page = Assert.IsType<ContentResult>(await controller.AppNew(state, Ct));
        Assert.StartsWith("text/html", page.ContentType);
        string Attribute(string pattern) => WebUtility.HtmlDecode(Regex.Match(page.Content!, pattern).Groups[1].Value);
        return (Attribute("action=\"([^\"]*)\""), JObject.Parse(Attribute("name=\"manifest\" value=\"([^\"]*)\"")));
    }
}
