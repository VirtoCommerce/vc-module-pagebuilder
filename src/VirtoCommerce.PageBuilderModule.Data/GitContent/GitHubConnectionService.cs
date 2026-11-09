using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>
/// GitHub connection: the configuration when it is complete, else the GitHub App of the one-click setup, kept in a
/// hidden setting encrypted with the platform's data protection keys.
/// </summary>
public class GitHubConnectionService(
    IOptions<GitContentOptions> options,
    IConfiguration configuration,
    ISettingsManager settingsManager,
    IDataProtectionProvider dataProtectionProvider,
    IGitHubApiClient gitHubApiClient,
    ILogger<GitHubConnectionService> logger)
    : IGitHubConnectionService
{
    /// <summary>Base branch of the store repositories when the configuration names none.</summary>
    public const string DefaultBaseBranch = "main";

    private const string ProtectionPurpose = "VirtoCommerce.PageBuilderModule.GitHubAppConnection";
    private const string GitHubApiHost = "api.github.com";
    private const string GitHubWebUrl = "https://github.com/";
    private const string AppDescription = "Keeps each site's Page Builder pages in its own repository.";

    // GitHub's limit on an App's name
    private const int MaxAppNameLength = 34;

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);

    // an explicit Enabled=false also switches off the saved App; a bound bool cannot tell it from "not set"
    private readonly bool _isSwitchedOff =
        bool.TryParse(configuration.GetSection(GitContentOptions.SectionName)[nameof(GitContentOptions.Enabled)], out var enabled) && !enabled;

    public virtual async Task<GitHubConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = CreateConfiguredConnection();
        if (connection.Source == GitHubConnectionSource.Configuration)
        {
            return connection;
        }

        string stored;
        try
        {
            stored = await settingsManager.GetValueAsync<string>(ModuleConstants.Settings.GitHub.AppConnection);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The saved GitHub connection could not be read.");
            return SetError(connection, "The saved GitHub connection could not be read. Try again in a moment.");
        }

        if (string.IsNullOrWhiteSpace(stored))
        {
            return connection;
        }

        GitHubAppConnection app;
        try
        {
            app = JsonConvert.DeserializeObject<GitHubAppConnection>(_protector.Unprotect(stored));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            logger.LogWarning(ex, "The saved GitHub connection cannot be decrypted on this server.");
            return SetError(connection, "The saved GitHub connection cannot be read on this server: it was made on another one, or this server's " +
                                        "data protection keys have changed. Set it up again.");
        }

        ApplyApp(connection, app);
        return connection;
    }

    public virtual async Task<GitHubAppConnection> CreateAppAsync(string code, string organization, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var operation = "convert the GitHub App manifest";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"app-manifests/{Uri.EscapeDataString(code)}/conversions");
        using var response = await gitHubApiClient.SendAnonymousAsync(request, operation, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GitContentException(
                $"GitHub did not hand the new App over ({(int)response.StatusCode} {response.ReasonPhrase}). " +
                "Its link works once and only for an hour: start the setup again from the site.");
        }

        var conversion = await GitHubApiClient.ReadAsAsync<AppConversion>(response, operation, cancellationToken);
        var app = AbstractTypeFactory<GitHubAppConnection>.TryCreateInstance();
        app.Organization = conversion?.Owner?.Login;
        app.AppId = conversion?.Id;
        app.AppSlug = conversion?.Slug;
        app.PrivateKey = conversion?.Pem;

        if (!string.Equals(app.Organization, organization, StringComparison.OrdinalIgnoreCase))
        {
            throw new GitContentException($"The App was created for \"{app.Organization}\", not for \"{organization}\". Start the setup again from the site.");
        }

        if (string.IsNullOrWhiteSpace(app.AppId) || string.IsNullOrWhiteSpace(app.AppSlug) || string.IsNullOrWhiteSpace(app.PrivateKey))
        {
            throw new GitContentException("GitHub's answer has no App id, name or key. Start the setup again from the site.");
        }

        await settingsManager.SetValueAsync(ModuleConstants.Settings.GitHub.AppConnection.Name, _protector.Protect(JsonConvert.SerializeObject(app)));
        return app;
    }

    public virtual GitHubAppManifest CreateAppManifest(string organization, string url, string redirectUrl, string setupUrl)
    {
        var name = $"Page Builder {organization}";

        var manifest = AbstractTypeFactory<GitHubAppManifest>.TryCreateInstance();
        manifest.Name = name.Length > MaxAppNameLength ? name[..MaxAppNameLength].TrimEnd('-', ' ') : name;
        manifest.Url = url;
        manifest.Description = AppDescription;
        manifest.RedirectUrl = redirectUrl;
        manifest.SetupUrl = setupUrl;
        manifest.SetupOnUpdate = true;
        manifest.IsPublic = false;
        // no webhook: GitHub refuses a webhook address it cannot reach, as a local platform's
        manifest.DefaultPermissions = GetAppPermissions();
        manifest.DefaultEvents = [];

        return manifest;
    }

    protected virtual IDictionary<string, string> GetAppPermissions() => new Dictionary<string, string>
    {
        ["administration"] = "write",
        ["contents"] = "write",
        ["pull_requests"] = "write",
        ["metadata"] = "read",
    };

    protected virtual GitHubConnection CreateConfiguredConnection()
    {
        var configured = options.Value;

        var connection = AbstractTypeFactory<GitHubConnection>.TryCreateInstance();
        connection.IsEnabled = configured.Enabled;
        connection.Repository = configured.Repository;
        connection.Organization = configured.Organization;
        connection.AppId = configured.AppId;
        connection.PrivateKey = configured.PrivateKey;
        connection.Token = configured.Token;
        connection.BaseBranch = configured.BaseBranch;
        connection.AutoCreateRepositories = configured.AutoCreateRepositories;
        connection.WebUrl = GetWebUrl(configured.ApiUrl);
        connection.Source = configured.IsConfigured ? GitHubConnectionSource.Configuration : null;

        return connection;
    }

    protected virtual void ApplyApp(GitHubConnection connection, GitHubAppConnection app)
    {
        connection.IsEnabled = !_isSwitchedOff;
        connection.Repository = null;
        connection.Organization = app.Organization;
        connection.AppId = app.AppId;
        connection.PrivateKey = app.PrivateKey;
        connection.BaseBranch = string.IsNullOrWhiteSpace(connection.BaseBranch) ? DefaultBaseBranch : connection.BaseBranch;
        // a new store gets its repository straight away
        connection.AutoCreateRepositories = true;
        connection.Source = GitHubConnectionSource.App;
        connection.App = app;
    }

    /// <summary>
    /// Marks the connection as a saved App that cannot be used, enabled unless the configuration switches git content off, so a
    /// store that opted in refuses git operations instead of falling back to the blob flow.
    /// </summary>
    protected virtual GitHubConnection SetError(GitHubConnection connection, string error)
    {
        connection.IsEnabled = !_isSwitchedOff;
        connection.Error = error;
        return connection;
    }

    /// <summary>Gets the GitHub web site of the API: github.com, or the GitHub Enterprise server itself.</summary>
    protected virtual Uri GetWebUrl(Uri apiUrl) =>
        string.Equals(apiUrl.Host, GitHubApiHost, StringComparison.OrdinalIgnoreCase)
            ? new Uri(GitHubWebUrl)
            : new Uri(apiUrl.GetLeftPart(UriPartial.Authority) + "/");

    private sealed class AppConversion
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("pem")]
        public string Pem { get; set; }

        [JsonProperty("owner")]
        public AppOwner Owner { get; set; }
    }

    private sealed class AppOwner
    {
        [JsonProperty("login")]
        public string Login { get; set; }
    }
}
