using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
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
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// The one-click setup: GitHub's create-an-App form, the way back with GitHub's code, and on to the
/// installation and back to the site.
/// </summary>
public class GitContentSetupControllerTests
{
    private readonly MemorySettingsManager _settings = new();
    private readonly EphemeralDataProtectionProvider _keys = new();

    [Fact]
    public async Task The_manifest_asks_for_what_site_repositories_need_and_comes_back_to_the_site()
    {
        var controller = Controller();
        var state = await State(controller, "acme", "B2B store");
        var (action, manifest) = await Form(controller, state);

        Assert.StartsWith("https://github.com/organizations/acme/settings/apps/new?state=", action);
        // the platform's pages may post forms only to themselves; this one also to GitHub
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
    public async Task A_long_organization_name_still_makes_an_App_name_GitHub_takes()
    {
        var controller = Controller();
        var (_, manifest) = await Form(controller, await State(controller, new string('a', 39), "s1"));

        Assert.True(manifest["name"]!.Value<string>()!.Length <= 34);
    }

    [Theory]
    [InlineData("")]
    [InlineData("acme/../settings")]
    [InlineData("-acme")]
    public async Task What_is_not_an_organization_name_is_refused(string organization)
    {
        Assert.IsType<BadRequestObjectResult>(await Controller().StartAppSetup(organization, "s1"));
    }

    [Fact]
    public async Task GitHubs_code_connects_the_App_and_goes_on_to_install_it()
    {
        var github = GitHubConnectionServiceTests.Conversion("acme");
        var connection = GitHubConnectionServiceTests.Service(_settings, _keys, github);
        var controller = Controller(connection);
        var result = await controller.AppCreated("c0de", await State(controller, "acme", "s1"));

        github.AssertDone();
        Assert.Equal("https://github.com/apps/page-builder-acme/installations/new", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("77", (await connection.GetConnectionAsync(TestContext.Current.CancellationToken)).App?.AppId);
    }

    [Fact]
    public async Task A_forged_state_is_refused_before_GitHub_is_asked()
    {
        var github = GitHubConnectionServiceTests.Conversion("acme");

        var result = await Controller(GitHubConnectionServiceTests.Service(_settings, _keys, github)).AppCreated("c0de", "forged");

        Assert.Equal(400, Assert.IsType<ContentResult>(result).StatusCode);
        Assert.Equal(400, Assert.IsType<ContentResult>(await Controller().AppNew("forged")).StatusCode);
        Assert.Equal(0, github.Requests);
        Assert.Empty(_settings.Values);
    }

    [Fact]
    public async Task After_the_installation_GitHub_sends_the_administrator_back_to_the_site()
    {
        var controller = Controller();
        var state = await State(controller, "acme", "B2B store");

        Assert.Equal("/apps/admin-shell/#/content/site/B2B%20store", Assert.IsType<RedirectResult>(controller.AppInstalled(state)).Url);
        Assert.Equal("/apps/admin-shell/#/content/site/B2B%20store", Assert.IsType<RedirectResult>(controller.AppInstalled($"{state}?installation_id=9")).Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("B2B store")]
    [InlineData("forged")]
    public void Without_a_valid_state_the_installation_lands_on_the_sites_panel(string state)
    {
        Assert.Equal("/apps/admin-shell/#/content", Assert.IsType<RedirectResult>(Controller().AppInstalled(state)).Url);
    }

    [Fact]
    public async Task The_site_status_says_the_connection_is_the_setups_App_and_where_to_install_it()
    {
        var connection = GitHubConnectionServiceTests.Service(_settings, _keys, GitHubConnectionServiceTests.Conversion("acme"));
        await connection.CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        var status = Assert.IsType<GitContentSiteStatus>(Assert.IsType<OkObjectResult>((await Controller(connection).GetSite("s1")).Result).Value);

        Assert.Equal(GitHubConnectionSource.App, status.Source);
        Assert.Equal("app", JObject.FromObject(status)[nameof(GitContentSiteStatus.Source)]?.Value<string>());
        Assert.Equal("https://github.com/apps/page-builder-acme/installations/new", status.InstallUrl);
    }

    [Fact]
    public async Task The_site_status_of_a_store_the_user_does_not_work_in_is_forbidden()
    {
        var controller = Controller(authorization: new StoreAuthorization("s2"));

        Assert.IsType<ForbidResult>((await controller.GetSite("s1")).Result);
    }

    [Fact]
    public async Task The_sites_panel_lists_the_stores_the_user_works_in()
    {
        var connection = GitHubConnectionServiceTests.Service(_settings, _keys, configure: options =>
        {
            options.Enabled = true;
            options.Organization = "acme";
            options.Token = "t0ken";
            options.BaseBranch = "main";
        });
        var controller = Controller(connection, new StoreAuthorization("s2"), new SitePolicy(), new Stores("s1", "s2"));

        var answer = JObject.FromObject(Assert.IsType<OkObjectResult>(await controller.GetConnection()).Value!);

        Assert.Equal(["s2"], answer["sites"]!.Select(x => x.Value<string>("storeId")));
    }

    [Fact]
    public async Task The_sites_panel_finds_the_users_store_past_the_first_page_of_stores()
    {
        var controller = Controller(ConfiguredConnection(), new StoreAuthorization("s150"), stores: new Stores(Enumerable.Range(1, 250).Select(x => $"s{x:000}").ToArray()));

        var answer = JObject.FromObject(Assert.IsType<OkObjectResult>(await controller.GetConnection()).Value!);

        Assert.Equal(["s150"], answer["sites"]!.Select(x => x.Value<string>("storeId")));
    }

    [Fact]
    public async Task A_GitHub_that_does_not_answer_in_time_is_what_the_connection_test_reports()
    {
        var provisioner = new FakeGitContentProvisioner { OrganizationFailure = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.") };
        var controller = Controller(ConfiguredConnection(), provisioner: provisioner);

        var answer = JObject.FromObject(Assert.IsType<OkObjectResult>(await controller.GetConnection()).Value!);

        Assert.Contains("HttpClient.Timeout", answer.Value<string>("error"));
    }

    [Fact]
    public async Task A_store_that_does_not_exist_is_not_connected()
    {
        var provisioner = new FakeGitContentProvisioner();

        var result = await Controller(provisioner: provisioner).ConnectSite("gone");

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(provisioner.Connected);
    }

    [Fact]
    public async Task A_store_the_user_does_not_work_in_is_not_connected()
    {
        var provisioner = new FakeGitContentProvisioner();

        var result = await Controller(authorization: new StoreAuthorization("s2"), provisioner: provisioner).ConnectSite("s1");

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(provisioner.Connected);
    }

    [Fact]
    public async Task With_the_configuration_in_charge_the_setup_is_not_offered()
    {
        var connection = GitHubConnectionServiceTests.Service(_settings, _keys, configure: options =>
        {
            options.Organization = "env-org";
            options.Token = "t0ken";
            options.BaseBranch = "main";
        });

        Assert.IsType<BadRequestObjectResult>(await Controller(connection).StartAppSetup("acme", "s1"));
    }

    [Fact]
    public async Task GetConnection_SetupApp_WritesTheSitesPanelJson()
    {
        var connection = GitHubConnectionServiceTests.Service(_settings, _keys, GitHubConnectionServiceTests.Conversion("acme"));
        await connection.CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        var body = PlatformJson.SerializeBody(await Controller(connection, stores: new Stores("s1", "s2")).GetConnection());

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
        Assert.Equal("""{"sites":[],"configured":false,"autoCreateRepositories":false}""", PlatformJson.SerializeBody(await Controller().GetConnection()));
    }

    [Fact]
    public async Task StartAppSetup_ValidOrganization_WritesTheSetupPageUrlJson()
    {
        var body = PlatformJson.ParseBody(await Controller().StartAppSetup("acme", "s1"));

        Assert.Equal(["url"], body.Properties().Select(x => x.Name));
        Assert.StartsWith("/api/pagebuilder/git/app/new?state=", body.Value<string>("url"));
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private GitHubConnectionService ConfiguredConnection() =>
        GitHubConnectionServiceTests.Service(_settings, _keys, configure: options =>
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

        return new GitContentSetupController(provisioner ?? new FakeGitContentProvisioner(), connection ?? GitHubConnectionServiceTests.Service(_settings, _keys), _keys,
            authorization ?? new FakeAuthorizationService(true), policy ?? new SitePolicy(), stores ?? new Stores(),
            RequestValidators.Stores("s1", "s2"), RequestValidators.AppSetup)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    /// <summary>Lets the user into the one store they work in, as Page Builder's store check does.</summary>
    private sealed class StoreAuthorization(string storeId) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(resource is IHasStoreId store && store.StoreId == storeId ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
            throw new NotSupportedException();
    }

    private sealed class SitePolicy : IGitContentPolicy
    {
        public Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task SetEnabledForStoreAsync(string storeId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetRepositoryAsync(string storeId, CancellationToken cancellationToken = default) => Task.FromResult(Of(storeId).Name);
        public Task<StoreRepository> GetRepositoryInfoAsync(string storeId, CancellationToken cancellationToken = default) => Task.FromResult(Of(storeId));
        public Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, CancellationToken cancellationToken = default) => Task.FromResult(Of(storeId));
        public Task<StoreRepository> GetConfiguredRepositoryAsync(string storeId, string repositorySetting, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> IsInOrganizationAsync(string repository, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private static StoreRepository Of(string storeId) => new($"acme/site-{storeId}", GitRepositorySource.Site);
    }

    private sealed class Stores(params string[] ids) : IStoreSearchService
    {
        public Task<StoreSearchResult> SearchAsync(StoreSearchCriteria criteria, bool clone = true) =>
            Task.FromResult(new StoreSearchResult
            {
                Results = ids.Skip(criteria.Skip).Take(criteria.Take).Select(id => new Store { Id = id, Name = id }).ToList(),
                TotalCount = ids.Length,
            });
    }

    /// <summary>The state the setup carries, from the address the panel opens.</summary>
    private static async Task<string> State(GitContentSetupController controller, string organization, string storeId)
    {
        var url = JObject.FromObject(Assert.IsType<OkObjectResult>(await controller.StartAppSetup(organization, storeId)).Value!)["url"]!.Value<string>()!;
        Assert.StartsWith("/api/pagebuilder/git/app/new?state=", url);
        return HttpUtility.ParseQueryString(url[url.IndexOf('?')..])["state"];
    }

    /// <summary>Where the page that opens posts, and the manifest it posts.</summary>
    private static async Task<(string Action, JObject Manifest)> Form(GitContentSetupController controller, string state)
    {
        var page = Assert.IsType<ContentResult>(await controller.AppNew(state));
        Assert.StartsWith("text/html", page.ContentType);
        string Attribute(string pattern) => WebUtility.HtmlDecode(Regex.Match(page.Content!, pattern).Groups[1].Value);
        return (Attribute("action=\"([^\"]*)\""), JObject.Parse(Attribute("name=\"manifest\" value=\"([^\"]*)\"")));
    }
}
