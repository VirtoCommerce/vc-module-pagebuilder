using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

public class GitContentSetupService(
    IGitHubConnectionService gitHubConnectionService,
    IGitContentProvisioner gitContentProvisioner,
    IGitContentPolicy gitContentPolicy,
    IDataProtectionProvider dataProtectionProvider)
    : IGitContentSetupService
{
    private const string StatePurpose = "VirtoCommerce.PageBuilderModule.GitHubAppSetup";
    private const string AppSettingsPathFormat = "organizations/{0}/settings/apps/{1}";
    private const string AppCreationPathFormat = "organizations/{0}/settings/apps/new?state={1}";
    private const string AppInstallPathFormat = "apps/{0}/installations/new";

    // creating the App and installing it is a few minutes' work on GitHub
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(30);

    private readonly ITimeLimitedDataProtector _stateProtector = dataProtectionProvider.CreateProtector(StatePurpose).ToTimeLimitedDataProtector();

    public virtual async Task<GitContentConnectionStatus> GetConnectionStatusAsync(bool live, CancellationToken cancellationToken = default)
    {
        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
        var organization = connection.Organization?.Trim();

        var status = AbstractTypeFactory<GitContentConnectionStatus>.TryCreateInstance();
        status.Configured = connection.IsEnabled && connection.IsConfigured;
        status.Source = connection.Source;
        status.Organization = organization;
        status.OrganizationUrl = string.IsNullOrEmpty(organization) ? null : new Uri(connection.WebUrl, Uri.EscapeDataString(organization)).AbsoluteUri;
        status.Repository = string.IsNullOrEmpty(organization) ? connection.Repository : null;
        status.AppSlug = connection.App?.AppSlug;
        status.AppUrl = connection.App == null ? null : GetAppSettingsUrl(connection.WebUrl, connection.App);
        status.InstallUrl = connection.App == null ? null : GetAppInstallUrl(connection.WebUrl, connection.App);
        status.AutoCreateRepositories = status.Configured && connection.AutoCreateRepositories;
        status.Error = connection.Error;

        if (live && status.Configured && status.Error == null)
        {
            status.Error = await GetOrganizationErrorAsync(cancellationToken);
        }

        return status;
    }

    public virtual async Task<IList<GitContentSite>> GetSitesAsync(IList<Store> stores, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stores);

        var webUrl = (await gitHubConnectionService.GetConnectionAsync(cancellationToken)).WebUrl;
        var sites = new List<GitContentSite>();
        foreach (var store in stores)
        {
            sites.Add(await GetSiteAsync(store, webUrl, cancellationToken));
        }

        return sites;
    }

    public virtual async Task<GitContentSiteStatus> GetSiteStatusAsync(string storeId, CancellationToken cancellationToken = default)
    {
        var status = await gitContentProvisioner.GetStatusAsync(storeId, cancellationToken);
        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
        status.Source = connection.Source;
        status.InstallUrl = connection.App == null ? null : GetAppInstallUrl(connection.WebUrl, connection.App);
        status.Error ??= connection.Error;

        return status;
    }

    public virtual async Task<bool> CanSetUpAppAsync(CancellationToken cancellationToken = default)
    {
        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
        return connection.Source != GitHubConnectionSource.Configuration;
    }

    public virtual string CreateSetupState(GitHubAppSetupRequest setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        return _stateProtector.Protect(JsonConvert.SerializeObject(setup), StateLifetime);
    }

    public virtual GitHubAppSetupRequest ReadSetupState(string state)
    {
        try
        {
            var setup = JsonConvert.DeserializeObject<GitHubAppSetupRequest>(_stateProtector.Unprotect(state ?? string.Empty));
            return setup?.Organization != null ? setup : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public virtual async Task<string> GetAppCreationUrlAsync(string organization, string state, CancellationToken cancellationToken = default)
    {
        var webUrl = (await gitHubConnectionService.GetConnectionAsync(cancellationToken)).WebUrl;
        var path = string.Format(CultureInfo.InvariantCulture, AppCreationPathFormat, Uri.EscapeDataString(organization), Uri.EscapeDataString(state));

        return new Uri(webUrl, path).AbsoluteUri;
    }

    public virtual async Task<string> GetAppInstallUrlAsync(GitHubAppConnection app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        var webUrl = (await gitHubConnectionService.GetConnectionAsync(cancellationToken)).WebUrl;
        return GetAppInstallUrl(webUrl, app);
    }

    protected virtual async Task<string> GetOrganizationErrorAsync(CancellationToken cancellationToken)
    {
        try
        {
            await gitContentProvisioner.CheckOrganizationAsync(cancellationToken);
            return null;
        }
        catch (GitContentException ex)
        {
            return ex.Message;
        }
    }

    protected virtual async Task<GitContentSite> GetSiteAsync(Store store, Uri webUrl, CancellationToken cancellationToken)
    {
        var site = AbstractTypeFactory<GitContentSite>.TryCreateInstance();
        site.StoreId = store.Id;
        site.Name = store.Name ?? store.Id;
        try
        {
            site.Repository = (await gitContentPolicy.GetConfiguredRepositoryAsync(store.Id, cancellationToken))?.Name;
            site.Enabled = await gitContentPolicy.IsEnabledForStoreAsync(store.Id, cancellationToken);
        }
        catch (GitContentException)
        {
            // a malformed repository setting: the site's own status reports it
        }

        site.Url = site.Repository == null ? null : new Uri(webUrl, site.Repository).AbsoluteUri;
        return site;
    }

    protected virtual string GetAppSettingsUrl(Uri webUrl, GitHubAppConnection app)
    {
        var path = string.Format(CultureInfo.InvariantCulture, AppSettingsPathFormat, Uri.EscapeDataString(app.Organization), Uri.EscapeDataString(app.AppSlug));
        return new Uri(webUrl, path).AbsoluteUri;
    }

    protected virtual string GetAppInstallUrl(Uri webUrl, GitHubAppConnection app)
    {
        var path = string.Format(CultureInfo.InvariantCulture, AppInstallPathFormat, Uri.EscapeDataString(app.AppSlug));
        return new Uri(webUrl, path).AbsoluteUri;
    }
}
