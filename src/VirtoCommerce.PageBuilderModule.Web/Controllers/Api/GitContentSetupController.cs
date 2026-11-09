using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Web.Extensions;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using StorePermissions = VirtoCommerce.StoreModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/pagebuilder/git")]
[Authorize]
[RepositoryOwnershipConflict]
public class GitContentSetupController(
    IGitContentSetupService gitContentSetupService,
    IGitContentProvisioner gitContentProvisioner,
    IGitHubConnectionService gitHubConnectionService,
    IAuthorizationService authorizationService,
    IStoreSearchService storeSearchService,
    IValidator<PageBuilderStoreRequest> storeRequestValidator,
    IValidator<GitHubAppSetupRequest> appSetupRequestValidator) : Controller
{
    // the key of Platform.Web's OverrideScpFormActionUri, which a module cannot reference
    private const string FormActionItem = "Content-Security-Policy-Form-Action-Uri";

    private const int MaxSites = 100;

    private const string ContentAppPath = "/apps/admin-shell/#/content";
    private const string SiteAppPathFormat = ContentAppPath + "/site/{0}";

    private const string ConfigurationInChargeError = "This platform takes its GitHub connection from its configuration.";
    private const string InvalidStateError = "This setup link has expired or is not valid. Start the setup again from the site.";
    private const string SetupErrorFormat = "GitHub setup did not finish: {0}";

    private const string AppCreationPage = """
        <!doctype html>
        <html>
        <head><meta charset="utf-8"><title>GitHub</title></head>
        <body>
        <form id="github" method="post" action="{0}">
        <input type="hidden" name="manifest" value="{1}">
        <noscript><button>Continue to GitHub</button></noscript>
        </form>
        <script>document.getElementById('github').submit()</script>
        </body>
        </html>
        """;

    /// <summary>Get git content status of the store</summary>
    [HttpGet]
    [Route("site")]
    public async Task<ActionResult<GitContentSiteStatus>> GetSite(string storeId, CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        return Ok(await gitContentSetupService.GetSiteStatusAsync(storeId, cancellationToken));
    }

    /// <summary>Enable git content for the store</summary>
    [HttpPost]
    [Route("site/connect")]
    public async Task<ActionResult<GitContentSiteStatus>> ConnectSite(string storeId, CancellationToken cancellationToken = default)
    {
        // store:update can be scoped to stores, which [Authorize] does not see
        if (await CheckRequestAsync(storeId, cancellationToken, StorePermissions.Update) is { } refused)
        {
            return refused;
        }

        try
        {
            await gitContentProvisioner.ConnectAsync(storeId, cancellationToken);
        }
        catch (GitContentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return Ok(await gitContentSetupService.GetSiteStatusAsync(storeId, cancellationToken));
    }

    /// <summary>Disable git content for the store</summary>
    [HttpPost]
    [Route("site/disconnect")]
    public async Task<ActionResult<GitContentSiteStatus>> DisconnectSite(string storeId, CancellationToken cancellationToken = default)
    {
        // store:update can be scoped to stores, which [Authorize] does not see
        if (await CheckRequestAsync(storeId, cancellationToken, StorePermissions.Update) is { } refused)
        {
            return refused;
        }

        await gitContentProvisioner.DisconnectAsync(storeId, cancellationToken);

        return Ok(await gitContentSetupService.GetSiteStatusAsync(storeId, cancellationToken));
    }

    /// <summary>Get GitHub connection status</summary>
    /// <param name="live">Check the connection on GitHub and list the sites</param>
    [HttpGet]
    [Route("connection")]
    public async Task<ActionResult<GitContentConnectionStatus>> GetConnection([FromQuery] bool live = true, CancellationToken cancellationToken = default)
    {
        // builder:read can be scoped to stores, which [Authorize] does not see
        var readsEverywhere = (await authorizationService.AuthorizeAsync(User, null, ModuleConstants.Security.Permissions.Read)).Succeeded;
        var stores = readsEverywhere ? null : await GetAccessibleStoresAsync();
        if (stores is { Count: 0 })
        {
            return Forbid();
        }

        var status = await gitContentSetupService.GetConnectionStatusAsync(live, cancellationToken);
        if (live && status.Configured)
        {
            status.Sites = await gitContentSetupService.GetSitesAsync(stores ?? await GetAccessibleStoresAsync(), cancellationToken);
        }

        return Ok(status);
    }

    /// <summary>Start GitHub App setup for the organization</summary>
    [HttpGet]
    [Route("app/setup")]
    [Authorize(PlatformConstants.Security.Permissions.SettingUpdate)]
    public async Task<ActionResult<GitHubAppSetupResult>> StartAppSetup(string organization, string storeId, CancellationToken cancellationToken = default)
    {
        if (!await gitContentSetupService.CanSetUpAppAsync(cancellationToken))
        {
            return BadRequest(new { error = ConfigurationInChargeError });
        }

        var setup = new GitHubAppSetupRequest { Organization = organization?.Trim(), StoreId = storeId };
        if (await appSetupRequestValidator.ValidateRequestAsync(setup, ModelState, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = AbstractTypeFactory<GitHubAppSetupResult>.TryCreateInstance();
        result.Url = Url.Action(nameof(AppNew), new { state = gitContentSetupService.CreateSetupState(setup) });

        return Ok(result);
    }

    /// <summary>Post the GitHub App manifest (anonymous: opened in a new tab, authorized by the state)</summary>
    [HttpGet]
    [Route("app/new")]
    [AllowAnonymous]
    public async Task<ActionResult> AppNew(string state, CancellationToken cancellationToken = default)
    {
        var setup = gitContentSetupService.ReadSetupState(state);
        if (setup == null)
        {
            return CreateSetupErrorResult(InvalidStateError);
        }

        var manifest = gitHubConnectionService.CreateAppManifest(
            setup.Organization,
            $"{Request.Scheme}://{Request.Host}{Request.PathBase}/",
            Url.Action(nameof(AppCreated), null, null, Request.Scheme),
            // GitHub appends its own query to the setup url
            Url.Action(nameof(AppInstalled), null, new { state }, Request.Scheme));

        var appCreationUrl = await gitContentSetupService.GetAppCreationUrlAsync(setup.Organization, state, cancellationToken);
        // the whole GitHub site: it may send the browser on to sign in first
        HttpContext.Items[FormActionItem] = new Uri(appCreationUrl).GetLeftPart(UriPartial.Authority);

        var page = string.Format(CultureInfo.InvariantCulture, AppCreationPage,
            WebUtility.HtmlEncode(appCreationUrl),
            WebUtility.HtmlEncode(JsonConvert.SerializeObject(manifest)));

        return Content(page, "text/html; charset=utf-8");
    }

    /// <summary>GitHub App created callback</summary>
    [HttpGet]
    [Route("app/created")]
    [AllowAnonymous]
    public async Task<ActionResult> AppCreated(string code, string state, CancellationToken cancellationToken = default)
    {
        if (!await gitContentSetupService.CanSetUpAppAsync(cancellationToken))
        {
            return CreateSetupErrorResult(ConfigurationInChargeError);
        }

        var setup = gitContentSetupService.ReadSetupState(state);
        if (setup == null)
        {
            return CreateSetupErrorResult(InvalidStateError);
        }

        GitHubAppConnection app;
        try
        {
            app = await gitHubConnectionService.CreateAppAsync(code, setup.Organization, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return CreateSetupErrorResult(ex.Message);
        }
        catch (GitContentException ex)
        {
            return CreateSetupErrorResult(ex.Message);
        }

        return Redirect(await gitContentSetupService.GetAppInstallUrlAsync(app, cancellationToken));
    }

    /// <summary>GitHub App installed callback</summary>
    [HttpGet]
    [Route("app/installed")]
    [AllowAnonymous]
    public ActionResult AppInstalled(string state)
    {
        // GitHub may append "?installation_id=..." to the state value
        var storeId = gitContentSetupService.ReadSetupState(state?.Split('?')[0])?.StoreId;

        return Redirect(Request.PathBase + (storeId.IsNullOrEmpty()
            ? ContentAppPath
            : string.Format(CultureInfo.InvariantCulture, SiteAppPathFormat, Uri.EscapeDataString(storeId))));
    }

    private async Task<IList<Store>> GetAccessibleStoresAsync()
    {
        var stores = new List<Store>();
        var criteria = AbstractTypeFactory<StoreSearchCriteria>.TryCreateInstance();
        criteria.Sort = nameof(Store.Name);
        criteria.Take = MaxSites;

        while (stores.Count < MaxSites)
        {
            var searchResult = await storeSearchService.SearchAsync(criteria);
            foreach (var store in searchResult.Results)
            {
                if (stores.Count < MaxSites && await CheckStoreAccessAsync(store) == null)
                {
                    stores.Add(store);
                }
            }

            criteria.Skip += criteria.Take;
            if (searchResult.Results.Count < criteria.Take || criteria.Skip >= searchResult.TotalCount)
            {
                break;
            }
        }

        return stores;
    }

    // access first, so a 400 does not reveal which stores exist
    private async Task<ActionResult> CheckRequestAsync(string storeId, CancellationToken cancellationToken, string permission = null)
    {
        var request = new PageBuilderStoreRequest { StoreId = storeId };

        return (permission == null ? null : await CheckStoreAccessAsync(request, permission))
               ?? await CheckStoreAccessAsync(request)
               ?? await storeRequestValidator.ValidateRequestAsync(request, ModelState, cancellationToken);
    }

    private async Task<ActionResult> CheckStoreAccessAsync(object resource, string permission = ModuleConstants.Security.Permissions.Read)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, resource, new PageBuilderAuthorizationRequirement(permission));
        return authorizationResult.Succeeded ? null : Forbid();
    }

    private static ContentResult CreateSetupErrorResult(string message) => new()
    {
        StatusCode = StatusCodes.Status400BadRequest,
        ContentType = "text/plain; charset=utf-8",
        Content = string.Format(CultureInfo.InvariantCulture, SetupErrorFormat, message),
    };
}
