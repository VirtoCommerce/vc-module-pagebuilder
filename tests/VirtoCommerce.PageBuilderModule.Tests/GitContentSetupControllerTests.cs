using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// The one-click setup: GitHub's create-an-App form, the way back with GitHub's code, and on to the
    /// installation and back to the site.
    /// </summary>
    public class GitContentSetupControllerTests
    {
        private readonly MemorySettingsManager _settings = new();
        private readonly EphemeralDataProtectionProvider _keys = new();

        [Fact]
        public void The_manifest_asks_for_what_site_repositories_need_and_comes_back_to_the_site()
        {
            var controller = Controller();
            var (action, manifest) = Form(controller, State(controller, "acme", "B2B store"));

            Assert.StartsWith("https://github.com/organizations/acme/settings/apps/new?state=", action);
            // the platform's pages may post forms only to themselves; this one also to GitHub
            Assert.Equal("https://github.com", controller.HttpContext.Items["Content-Security-Policy-Form-Action-Uri"]);

            Assert.Equal("Page Builder acme", manifest["name"]?.Value<string>());
            Assert.False(manifest["public"]?.Value<bool>());
            Assert.Equal("https://platform.test/api/pagebuilder/git/app/created", manifest["redirect_url"]?.Value<string>());
            Assert.Equal("https://platform.test/api/pagebuilder/git/app/installed?storeId=B2B%20store", manifest["setup_url"]?.Value<string>());
            Assert.Null(manifest["hook_attributes"]);
            Assert.Equal(
                JObject.Parse("""{ "administration": "write", "contents": "write", "pull_requests": "write", "metadata": "read" }"""),
                manifest["default_permissions"],
                JToken.EqualityComparer);
        }

        [Fact]
        public void A_long_organization_name_still_makes_an_App_name_GitHub_takes()
        {
            var controller = Controller();
            var (_, manifest) = Form(controller, State(controller, new string('a', 39), "s1"));

            Assert.True(manifest["name"]!.Value<string>()!.Length <= 34);
        }

        [Theory]
        [InlineData("")]
        [InlineData("acme/../settings")]
        [InlineData("-acme")]
        public void What_is_not_an_organization_name_is_refused(string organization)
        {
            Assert.IsType<BadRequestObjectResult>(Controller().StartAppSetup(organization, "s1"));
        }

        [Fact]
        public async Task GitHubs_code_connects_the_App_and_goes_on_to_install_it()
        {
            var github = GitContentConnectionTests.Conversion("acme");
            var connection = GitContentConnectionTests.Connection(_settings, _keys, github);
            var controller = Controller(connection);
            var result = await controller.AppCreated("c0de", State(controller, "acme", "s1"));

            github.AssertDone();
            Assert.Equal("https://github.com/apps/page-builder-acme/installations/new", Assert.IsType<RedirectResult>(result).Url);
            Assert.Equal("77", connection.App?.AppId);
        }

        [Fact]
        public async Task A_forged_state_is_refused_before_GitHub_is_asked()
        {
            var github = GitContentConnectionTests.Conversion("acme");

            var result = await Controller(GitContentConnectionTests.Connection(_settings, _keys, github)).AppCreated("c0de", "forged");

            Assert.Equal(400, Assert.IsType<ContentResult>(result).StatusCode);
            Assert.Equal(400, Assert.IsType<ContentResult>(Controller().AppNew("forged")).StatusCode);
            Assert.Equal(0, github.Requests);
            Assert.Empty(_settings.Values);
        }

        [Fact]
        public void After_the_installation_GitHub_sends_the_administrator_back_to_the_site()
        {
            Assert.Equal("/apps/admin-shell/#/content/site/B2B%20store", Assert.IsType<RedirectResult>(Controller().AppInstalled("B2B store")).Url);
            Assert.Equal("/apps/admin-shell/#/content/site/s1", Assert.IsType<RedirectResult>(Controller().AppInstalled("s1?installation_id=9")).Url);
        }

        [Fact]
        public async Task The_site_status_says_the_connection_is_the_setups_App_and_where_to_install_it()
        {
            var connection = GitContentConnectionTests.Connection(_settings, _keys, GitContentConnectionTests.Conversion("acme"));
            await connection.CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

            var status = Assert.IsType<GitContentSiteStatus>(Assert.IsType<OkObjectResult>((await Controller(connection).GetSite("s1")).Result).Value);

            Assert.Equal("app", status.Source);
            Assert.Equal("https://github.com/apps/page-builder-acme/installations/new", status.InstallUrl);
        }

        [Fact]
        public void With_the_configuration_in_charge_the_setup_is_not_offered()
        {
            var connection = GitContentConnectionTests.Connection(_settings, _keys, configure: options =>
            {
                options.Organization = "env-org";
                options.Token = "t0ken";
                options.BaseBranch = "main";
            });

            Assert.IsType<BadRequestObjectResult>(Controller(connection).StartAppSetup("acme", "s1"));
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private GitContentSetupController Controller(GitContentConnection connection = null)
        {
            var http = new DefaultHttpContext();
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("platform.test");

            return new GitContentSetupController(new FakeGitContentProvisioner(), connection ?? GitContentConnectionTests.Connection(_settings, _keys), _keys)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
            };
        }

        /// <summary>The state the setup carries, from the address the panel opens.</summary>
        private static string State(GitContentSetupController controller, string organization, string storeId)
        {
            var url = JObject.FromObject(Assert.IsType<OkObjectResult>(controller.StartAppSetup(organization, storeId)).Value!)["url"]!.Value<string>()!;
            Assert.StartsWith("/api/pagebuilder/git/app/new?state=", url);
            return HttpUtility.ParseQueryString(url[url.IndexOf('?')..])["state"];
        }

        /// <summary>Where the page that opens posts, and the manifest it posts.</summary>
        private static (string Action, JObject Manifest) Form(GitContentSetupController controller, string state)
        {
            var page = Assert.IsType<ContentResult>(controller.AppNew(state));
            Assert.StartsWith("text/html", page.ContentType);
            string Attribute(string pattern) => WebUtility.HtmlDecode(Regex.Match(page.Content!, pattern).Groups[1].Value);
            return (Attribute("action=\"([^\"]*)\""), JObject.Parse(Attribute("name=\"manifest\" value=\"([^\"]*)\"")));
        }
    }
}
