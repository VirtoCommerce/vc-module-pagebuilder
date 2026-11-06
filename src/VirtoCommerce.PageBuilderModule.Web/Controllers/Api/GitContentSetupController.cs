using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using StorePermissions = VirtoCommerce.StoreModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

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
[RepositoryOwnershipConflict]
public class GitContentSetupController(
    IGitContentProvisioner gitContentProvisioner,
    IGitHubConnectionService gitHubConnectionService,
    IDataProtectionProvider dataProtectionProvider,
    IAuthorizationService authorizationService,
    IGitContentPolicy gitContentPolicy,
    IStoreSearchService storeSearchService,
    IValidator<PageBuilderStoreRequest> storeRequestValidator,
    IValidator<GitHubAppSetupRequest> appSetupRequestValidator) : Controller
{
    private const string StatePurpose = "VirtoCommerce.PageBuilderModule.GitHubAppSetup";

    // The platform's Content-Security-Policy lets forms post to its own pages only ("form-action 'self'");
    // a response may name one more address under this HttpContext item — the platform's own sign-in
    // consent page does it for its redirect.
    private const string FormActionItem = "Content-Security-Policy-Form-Action-Uri";

    // as many sites as the Sites panel shows
    private const int MaxSites = 100;

    // creating the App and installing it is a few minutes' work on GitHub
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The store's version history: its repository, whether it is on, what GitHub says about it right now
    /// (asked live every time, so it is also the connection test), and where the connection comes from.
    /// </summary>
    [HttpGet]
    [Route("site")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GitContentSiteStatus>> GetSite(string storeId)
    {
        if (await CheckRequestAsync(storeId) is { } refused)
        {
            return refused;
        }

        return Ok(await GetStatusAsync(storeId));
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
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult> GetConnection([FromQuery] bool live = true)
    {
        var connection = await gitHubConnectionService.GetConnectionAsync(HttpContext.RequestAborted);
        var configured = connection.IsEnabled && connection.IsConfigured;
        var error = connection.Error;
        if (live && configured && error == null)
        {
            try
            {
                await gitContentProvisioner.CheckOrganizationAsync(HttpContext.RequestAborted);
            }
            catch (Exception failure) when (GitHubFailure.Is(failure, HttpContext.RequestAborted))
            {
                error = failure.Message;
            }
        }

        var app = connection.App;
        var web = connection.WebUrl;
        var organization = connection.Organization?.Trim();
        return Ok(new
        {
            sites = live && configured ? await SitesAsync(web) : [],
            configured,
            source = connection.Source,
            organization,
            organizationUrl = string.IsNullOrEmpty(organization) ? null : new Uri(web, Uri.EscapeDataString(organization)).AbsoluteUri,
            repository = string.IsNullOrEmpty(organization) ? connection.Repository : null,
            appSlug = app?.AppSlug,
            appUrl = app == null ? null : new Uri(web, $"organizations/{Uri.EscapeDataString(app.Organization)}/settings/apps/{Uri.EscapeDataString(app.AppSlug)}").AbsoluteUri,
            installUrl = app == null ? null : GetInstallUrl(web, app),
            autoCreateRepositories = configured && connection.AutoCreateRepositories,
            error,
        });
    }

    /// <summary>
    /// The first sites by name the user works in, with their repository and whether their history is on: settings
    /// only, no GitHub calls. The stores are read a page at a time until the list is full, as a user who works in
    /// a few stores may find them anywhere in the order.
    /// </summary>
    private async Task<IList<object>> SitesAsync(Uri web)
    {
        var sites = new List<object>();
        var criteria = new StoreSearchCriteria { Sort = "Name", Take = MaxSites };
        while (sites.Count < MaxSites)
        {
            var stores = await storeSearchService.SearchAsync(criteria);
            foreach (var store in stores.Results)
            {
                if (sites.Count < MaxSites && await this.CheckStoreAccessAsync(authorizationService, new PageBuilderStoreRequest { StoreId = store.Id }) == null)
                {
                    sites.Add(await SiteAsync(store.Id, store.Name, web));
                }
            }

            criteria.Skip += criteria.Take;
            if (stores.Results.Count < criteria.Take || criteria.Skip >= stores.TotalCount)
            {
                break;
            }
        }

        return sites;
    }

    private async Task<object> SiteAsync(string storeId, string name, Uri web)
    {
        string repository = null;
        var enabled = false;
        try
        {
            repository = (await gitContentPolicy.GetConfiguredRepositoryAsync(storeId, HttpContext.RequestAborted))?.Name;
            enabled = await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted);
        }
        catch (GitContentException)
        {
            // a malformed repository setting: the site's own panel says what is wrong with it
        }

        return new
        {
            storeId,
            name = name ?? storeId,
            repository,
            url = repository == null ? null : new Uri(web, repository).AbsoluteUri,
            enabled,
        };
    }

    /// <summary>
    /// Starts the one-click setup for <paramref name="organization"/>: the address of <see cref="AppNew"/>
    /// for it, to open in a tab of its own. It connects the whole platform, so it takes the right to change
    /// settings.
    /// </summary>
    [HttpGet]
    [Route("app/setup")]
    [Authorize(PlatformConstants.Security.Permissions.SettingUpdate)]
    public async Task<ActionResult> StartAppSetup(string organization, string storeId)
    {
        if (await IsFromConfigurationAsync())
        {
            return BadRequest(new { error = "This platform takes its GitHub connection from its configuration." });
        }

        var setup = new GitHubAppSetupRequest { Organization = organization?.Trim(), StoreId = storeId };
        var validation = appSetupRequestValidator.Validate(setup);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = string.Join(" ", validation.Errors.Select(x => x.ErrorMessage)) });
        }

        // goes along to GitHub and comes back with its code: proves the code answers a setup started here
        var state = StateProtector().Protect(JsonConvert.SerializeObject(new SetupState(setup.Organization, setup.StoreId)), StateLifetime);
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
    public async Task<ActionResult> AppNew(string state)
    {
        if (!TryReadState(state, out var setup))
        {
            return Failure("This setup link has expired or is not valid. Start the setup again from the site.");
        }

        var platform = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        // GitHub adds its own query to the setup address; the site to go back to rides in the state
        var manifest = gitHubConnectionService.CreateAppManifest(
            setup.Organization,
            $"{platform}/",
            $"{platform}/api/pagebuilder/git/app/created",
            $"{platform}/api/pagebuilder/git/app/installed?state={Uri.EscapeDataString(state)}");

        var web = (await gitHubConnectionService.GetConnectionAsync(HttpContext.RequestAborted)).WebUrl;
        var action = new Uri(web, $"organizations/{setup.Organization}/settings/apps/new?state={Uri.EscapeDataString(state)}");
        // GitHub as well — the whole site: it may send the browser on to sign in first
        HttpContext.Items[FormActionItem] = web.GetLeftPart(UriPartial.Authority);

        return Content(
            "<!doctype html><html><head><meta charset=\"utf-8\"><title>GitHub</title></head><body>" +
            $"<form id=\"github\" method=\"post\" action=\"{WebUtility.HtmlEncode(action.AbsoluteUri)}\">" +
            $"<input type=\"hidden\" name=\"manifest\" value=\"{WebUtility.HtmlEncode(JsonConvert.SerializeObject(manifest))}\">" +
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
        if (await IsFromConfigurationAsync())
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
            app = await gitHubConnectionService.CreateAppAsync(code, setup.Organization, HttpContext.RequestAborted);
        }
        catch (Exception failure) when (failure is ArgumentException or JsonException || GitHubFailure.Is(failure, HttpContext.RequestAborted))
        {
            return Failure(failure.Message);
        }

        var web = (await gitHubConnectionService.GetConnectionAsync(HttpContext.RequestAborted)).WebUrl;
        return Redirect(GetInstallUrl(web, app));
    }

    /// <summary>
    /// GitHub's way back once the App is installed: to the site the setup started from, else the Sites panel.
    /// Anonymous, because it is GitHub that sends the browser here; only a valid state names a site, and a
    /// later install of the same App (its state long expired) lands on the Sites panel.
    /// </summary>
    [HttpGet]
    [Route("app/installed")]
    [AllowAnonymous]
    public ActionResult AppInstalled(string state)
    {
        // in case GitHub glued its own "?installation_id=..." on rather than adding to the query
        var site = TryReadState(state?.Split('?')[0], out var setup) ? setup.StoreId : null;
        return Redirect(site.IsNullOrEmpty()
            ? $"{Request.PathBase}/apps/admin-shell/#/content"
            : $"{Request.PathBase}/apps/admin-shell/#/content/site/{Uri.EscapeDataString(site)}");
    }

    private async Task<ActionResult<GitContentSiteStatus>> ChangeSiteAsync(string storeId, Func<Task> change)
    {
        if (await CheckRequestAsync(storeId) is { } refused)
        {
            return refused;
        }

        try
        {
            await change();
        }
        catch (Exception failure) when (GitHubFailure.Is(failure, HttpContext.RequestAborted))
        {
            return BadRequest(new { error = failure.Message });
        }

        return Ok(await GetStatusAsync(storeId));
    }

    private Task<ActionResult> CheckRequestAsync(string storeId) =>
        this.CheckStoreRequestAsync(authorizationService, storeRequestValidator, new PageBuilderStoreRequest { StoreId = storeId });

    private async Task<GitContentSiteStatus> GetStatusAsync(string storeId)
    {
        var status = await gitContentProvisioner.GetStatusAsync(storeId, HttpContext.RequestAborted);
        var connection = await gitHubConnectionService.GetConnectionAsync(HttpContext.RequestAborted);
        status.Source = connection.Source;
        status.InstallUrl = connection.App == null ? null : GetInstallUrl(connection.WebUrl, connection.App);
        status.Error ??= connection.Error;
        return status;
    }

    private async Task<bool> IsFromConfigurationAsync() =>
        (await gitHubConnectionService.GetConnectionAsync(HttpContext.RequestAborted)).Source == GitHubConnectionSource.Configuration;

    private static string GetInstallUrl(Uri web, GitHubAppConnection app) =>
        new Uri(web, $"apps/{Uri.EscapeDataString(app.AppSlug)}/installations/new").AbsoluteUri;

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

    private static ContentResult Failure(string message) => new()
    {
        StatusCode = 400,
        ContentType = "text/plain; charset=utf-8",
        Content = $"GitHub setup did not finish: {message}",
    };
}
