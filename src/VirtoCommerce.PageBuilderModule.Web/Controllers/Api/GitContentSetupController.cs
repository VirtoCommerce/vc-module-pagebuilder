using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using StorePermissions = VirtoCommerce.StoreModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    /// <summary>
    /// A site's version history on GitHub: its status, switching it on and off, and the one-click setup that
    /// connects the platform to the customer's GitHub organization.
    /// <para>
    /// The setup is GitHub's App manifest flow. The site panel opens <see cref="AppNew"/> in a tab of its own,
    /// which posts the manifest to GitHub, where the administrator creates the App in their organization;
    /// GitHub sends them back to <see cref="AppCreated"/>
    /// with a one-time code the platform exchanges for the App's id and key, then on to install the App on the
    /// organization, after which GitHub returns them to the site through <see cref="AppInstalled"/>.
    /// </para>
    /// </summary>
    [Route("api/pagebuilder/git")]
    [Authorize]
    public class GitContentSetupController(
        IGitContentProvisioner gitContentProvisioner,
        GitContentConnection gitContentConnection,
        IDataProtectionProvider dataProtectionProvider,
        IGitContentPolicy gitContentPolicy = null,
        IStoreSearchService storeSearchService = null) : Controller
    {
        private const string StatePurpose = "VirtoCommerce.PageBuilderModule.GitHubAppSetup";

        // The platform's Content-Security-Policy lets forms post to its own pages only ("form-action 'self'");
        // a response may name one more address under this HttpContext item — the platform's own sign-in
        // consent page does it for its redirect.
        private const string FormActionItem = "Content-Security-Policy-Form-Action-Uri";

        // GitHub's limit on an App's name
        private const int MaxAppNameLength = 34;

        // creating the App and installing it is a few minutes' work on GitHub
        private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(30);

        // a GitHub account name; it ends up in the url of GitHub's form
        private static readonly Regex OrganizationPattern = new("^[A-Za-z0-9][A-Za-z0-9-]{0,38}$", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>
        /// The store's version history: its repository, whether it is on, what GitHub says about it right now
        /// (asked live every time, so it is also the connection test), and where the connection comes from.
        /// </summary>
        [HttpGet]
        [Route("site")]
        public async Task<ActionResult<GitContentSiteStatus>> GetSite(string storeId)
        {
            if (storeId.IsNullOrEmpty())
            {
                return BadRequest(new { error = "storeId is required." });
            }

            return Ok(await StatusAsync(storeId));
        }

        /// <summary>
        /// Switches the store's history on: its repository is created in the organization (or adopted, or
        /// brought back from the archive) and made ready, then the store's switch is turned on. Store settings
        /// are what this changes, so it takes the right to change a store.
        /// </summary>
        [HttpPost]
        [Route("site/connect")]
        [Authorize(StorePermissions.Update)]
        public Task<ActionResult<GitContentSiteStatus>> ConnectSite(string storeId) =>
            ChangeSiteAsync(storeId, () => gitContentProvisioner.ConnectAsync(storeId, HttpContext.RequestAborted));

        /// <summary>Switches the store's history off. The repository and its history stay.</summary>
        [HttpPost]
        [Route("site/disconnect")]
        [Authorize(StorePermissions.Update)]
        public Task<ActionResult<GitContentSiteStatus>> DisconnectSite(string storeId) =>
            ChangeSiteAsync(storeId, () => gitContentProvisioner.DisconnectAsync(storeId, HttpContext.RequestAborted));

        /// <summary>
        /// The platform's GitHub connection, for the Sites panel: where it comes from, the organization and
        /// the App, and what GitHub says about them right now — asked live, so it is also the connection test.
        /// <paramref name="live"/> false answers from the settings alone, without asking GitHub or listing the
        /// sites: what the Sites panel's button needs to show whether history is set up.
        /// </summary>
        [HttpGet]
        [Route("connection")]
        public async Task<ActionResult> GetConnection([FromQuery] bool live = true)
        {
            var options = gitContentConnection.Value;
            var configured = options.Enabled && options.IsConfigured;
            var error = gitContentConnection.Error;
            if (live && configured && error == null)
            {
                try
                {
                    await gitContentProvisioner.CheckOrganizationAsync(HttpContext.RequestAborted);
                }
                catch (Exception failure) when (failure is HttpRequestException or InvalidOperationException)
                {
                    error = failure.Message;
                }
            }

            var app = gitContentConnection.FromConfiguration ? null : gitContentConnection.App;
            var web = options.WebUrl;
            var organization = options.Organization?.Trim();
            return Ok(new
            {
                sites = live && configured ? await SitesAsync(web) : [],
                configured,
                source = Source(app),
                organization,
                organizationUrl = string.IsNullOrEmpty(organization) ? null : new Uri(web, Uri.EscapeDataString(organization)).AbsoluteUri,
                repository = string.IsNullOrEmpty(organization) ? options.Repository : null,
                appSlug = app?.AppSlug,
                appUrl = app == null ? null : new Uri(web, $"organizations/{Uri.EscapeDataString(app.Organization)}/settings/apps/{Uri.EscapeDataString(app.AppSlug)}").AbsoluteUri,
                installUrl = app == null ? null : InstallUrl(app),
                autoCreateRepositories = configured && options.AutoCreateRepositories,
                error,
            });
        }

        /// <summary>Every site with its repository and whether its history is on: settings only, no GitHub calls.</summary>
        private async Task<IList<object>> SitesAsync(Uri web)
        {
            var sites = new List<object>();
            if (gitContentPolicy == null || storeSearchService == null)
            {
                return sites;
            }

            // ponytail: one page of 100, as the Sites panel lists them; page through when a tenant runs more
            var stores = await storeSearchService.SearchAsync(new StoreSearchCriteria { Take = 100, Sort = "Name" });
            foreach (var store in stores.Results)
            {
                string repository = null;
                var enabled = false;
                try
                {
                    repository = await gitContentPolicy.ResolveRepositoryAsync(store.Id, HttpContext.RequestAborted);
                    enabled = await gitContentPolicy.IsEnabledForStoreAsync(store.Id, HttpContext.RequestAborted);
                }
                catch (InvalidOperationException)
                {
                    // a malformed repository setting: the site's own panel says what is wrong with it
                }

                sites.Add(new
                {
                    storeId = store.Id,
                    name = store.Name ?? store.Id,
                    repository,
                    url = repository == null ? null : new Uri(web, repository).AbsoluteUri,
                    enabled,
                });
            }

            return sites;
        }

        /// <summary>
        /// Starts the one-click setup for <paramref name="organization"/>: the address of <see cref="AppNew"/>
        /// for it, to open in a tab of its own. It connects the whole platform, so it takes the right to change
        /// settings.
        /// </summary>
        [HttpGet]
        [Route("app/setup")]
        [Authorize(PlatformConstants.Security.Permissions.SettingUpdate)]
        public ActionResult StartAppSetup(string organization, string storeId)
        {
            if (gitContentConnection.FromConfiguration)
            {
                return BadRequest(new { error = "This platform takes its GitHub connection from its configuration." });
            }

            organization = organization?.Trim();
            if (organization.IsNullOrEmpty() || !OrganizationPattern.IsMatch(organization))
            {
                return BadRequest(new { error = "Enter the name of a GitHub organization, as in github.com/<organization>." });
            }

            // goes along to GitHub and comes back with its code: proves the code answers a setup started here
            var state = StateProtector().Protect(JsonConvert.SerializeObject(new SetupState(organization, storeId)), StateLifetime);
            return Ok(new { url = $"{Request.PathBase}/api/pagebuilder/git/app/new?state={Uri.EscapeDataString(state)}" });
        }

        /// <summary>
        /// GitHub's "create an App" form, posted at once: the manifest gives the App what the site repositories
        /// need — creating them, their content and pull requests — and no webhook. A page of the platform's
        /// own, because the admin's pages may not post forms anywhere else. Anonymous, since a new tab carries
        /// no sign-in; the state from <see cref="StartAppSetup"/> is what allows it.
        /// </summary>
        [HttpGet]
        [Route("app/new")]
        [AllowAnonymous]
        public ActionResult AppNew(string state)
        {
            if (!TryReadState(state, out var setup))
            {
                return Failure("This setup link has expired or is not valid. Start the setup again from the site.");
            }

            var platform = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var manifest = new JObject
            {
                ["name"] = Truncate($"Page Builder {setup.Organization}", MaxAppNameLength),
                ["url"] = $"{platform}/",
                ["description"] = "Keeps each site's Page Builder pages in its own repository.",
                ["redirect_url"] = $"{platform}/api/pagebuilder/git/app/created",
                // GitHub adds its own query to this address; the site to go back to has to be part of it
                ["setup_url"] = setup.StoreId.IsNullOrEmpty()
                    ? $"{platform}/api/pagebuilder/git/app/installed"
                    : $"{platform}/api/pagebuilder/git/app/installed?storeId={Uri.EscapeDataString(setup.StoreId)}",
                ["setup_on_update"] = true,
                ["public"] = false,
                // no "hook_attributes": no webhook — and GitHub refuses a webhook address it cannot reach, as a local platform's
                ["default_permissions"] = new JObject
                {
                    ["administration"] = "write",
                    ["contents"] = "write",
                    ["pull_requests"] = "write",
                    ["metadata"] = "read",
                },
                ["default_events"] = new JArray(),
            };

            var web = gitContentConnection.Value.WebUrl;
            var action = new Uri(web, $"organizations/{setup.Organization}/settings/apps/new?state={Uri.EscapeDataString(state)}");
            // GitHub as well — the whole site: it may send the browser on to sign in first
            HttpContext.Items[FormActionItem] = web.GetLeftPart(UriPartial.Authority);

            return Content(
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>GitHub</title></head><body>" +
                $"<form id=\"github\" method=\"post\" action=\"{WebUtility.HtmlEncode(action.AbsoluteUri)}\">" +
                $"<input type=\"hidden\" name=\"manifest\" value=\"{WebUtility.HtmlEncode(manifest.ToString(Formatting.None))}\">" +
                "<noscript><button>Continue to GitHub</button></noscript></form>" +
                "<script>document.getElementById('github').submit()</script></body></html>",
                "text/html; charset=utf-8");
        }

        /// <summary>
        /// GitHub's way back once the App is created: the platform takes the App's id and key for the code,
        /// keeps them, and sends the administrator on to install the App on the organization. Anonymous,
        /// because it is GitHub that sends the browser here; the state proves the setup started on this platform.
        /// </summary>
        [HttpGet]
        [Route("app/created")]
        [AllowAnonymous]
        public async Task<ActionResult> AppCreated(string code, string state)
        {
            if (gitContentConnection.FromConfiguration)
            {
                return Failure("This platform takes its GitHub connection from its configuration.");
            }

            if (!TryReadState(state, out var setup))
            {
                return Failure("This setup link has expired or is not valid. Start the setup again from the site.");
            }

            GitHubAppConnection app;
            try
            {
                app = await gitContentConnection.CreateAppAsync(code, setup.Organization, HttpContext.RequestAborted);
            }
            catch (Exception failure) when (failure is ArgumentException or HttpRequestException or InvalidOperationException or JsonException)
            {
                return Failure(failure.Message);
            }

            return Redirect(InstallUrl(app));
        }

        /// <summary>GitHub's way back once the App is installed: to the site the setup started from, else the Sites panel.</summary>
        [HttpGet]
        [Route("app/installed")]
        [AllowAnonymous]
        public ActionResult AppInstalled(string storeId)
        {
            // in case GitHub glued its own "?installation_id=..." on rather than adding to the query
            var site = storeId?.Split('?')[0];
            return Redirect(site.IsNullOrEmpty()
                ? $"{Request.PathBase}/apps/admin-shell/#/content"
                : $"{Request.PathBase}/apps/admin-shell/#/content/site/{Uri.EscapeDataString(site)}");
        }

        private async Task<ActionResult<GitContentSiteStatus>> ChangeSiteAsync(string storeId, Func<Task> change)
        {
            if (storeId.IsNullOrEmpty())
            {
                return BadRequest(new { error = "storeId is required." });
            }

            try
            {
                await change();
            }
            catch (Exception failure) when (failure is HttpRequestException or InvalidOperationException)
            {
                return BadRequest(new { error = failure.Message });
            }

            return Ok(await StatusAsync(storeId));
        }

        private async Task<GitContentSiteStatus> StatusAsync(string storeId)
        {
            var status = await gitContentProvisioner.GetStatusAsync(storeId, HttpContext.RequestAborted);
            var app = gitContentConnection.FromConfiguration ? null : gitContentConnection.App;
            return status with
            {
                Source = Source(app),
                InstallUrl = app == null ? null : InstallUrl(app),
                Error = status.Error ?? gitContentConnection.Error,
            };
        }

        private string Source(GitHubAppConnection app)
        {
            if (gitContentConnection.FromConfiguration)
            {
                return "configuration";
            }

            return app == null ? null : "app";
        }

        private string InstallUrl(GitHubAppConnection app) =>
            new Uri(gitContentConnection.Value.WebUrl, $"apps/{Uri.EscapeDataString(app.AppSlug)}/installations/new").AbsoluteUri;

        private ITimeLimitedDataProtector StateProtector() =>
            dataProtectionProvider.CreateProtector(StatePurpose).ToTimeLimitedDataProtector();

        private bool TryReadState(string state, out SetupState setup)
        {
            try
            {
                setup = JsonConvert.DeserializeObject<SetupState>(StateProtector().Unprotect(state ?? string.Empty));
                return setup?.Organization != null;
            }
            catch (CryptographicException)
            {
                setup = null;
                return false;
            }
        }

        private sealed record SetupState(string Organization, string StoreId);

        private static string Truncate(string value, int length) => value.Length > length ? value[..length].TrimEnd('-', ' ') : value;

        private static ContentResult Failure(string message) => new()
        {
            StatusCode = 400,
            ContentType = "text/plain; charset=utf-8",
            Content = $"GitHub setup did not finish: {message}",
        };
    }
}
