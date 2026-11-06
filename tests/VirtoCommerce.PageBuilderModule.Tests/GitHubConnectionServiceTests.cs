using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// The connection in effect: the environment's configuration when complete, else the GitHub App the
/// one-click setup saved — protected with the platform's keys, and never a reason to fail.
/// </summary>
public class GitHubConnectionServiceTests
{
    private static readonly string Pem = RSA.Create(2048).ExportPkcs8PrivateKeyPem();

    [Fact]
    public async Task GetConnectionAsync_NoSavedApp_ReturnsConfiguration()
    {
        var connection = await Service(new MemorySettingsManager()).GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.False(connection.IsConfigured);
        Assert.Null(connection.Source);
        Assert.Null(connection.App);
        Assert.Null(connection.Error);
    }

    [Fact]
    public async Task GetConnectionAsync_EnabledFalseInConfiguration_SwitchesSavedAppOff()
    {
        var settings = new MemorySettingsManager();
        var keys = new EphemeralDataProtectionProvider();
        await Service(settings, keys, Conversion("Acme")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [$"{GitContentOptions.SectionName}:Enabled"] = "false" })
            .Build();

        var connection = await Service(settings, keys, configuration: configuration, configure: x => x.Enabled = false)
            .GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.True(connection.UsesApp);
        Assert.False(connection.IsEnabled);
    }

    [Fact]
    public async Task CreateAppAsync_CodeOfTheOrganization_SavesTheAppEncryptedAndConnectsWithIt()
    {
        var settings = new MemorySettingsManager();
        var keys = new EphemeralDataProtectionProvider();
        var github = Conversion("Acme");

        await Service(settings, keys, github).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        github.AssertDone();
        var saved = (string)settings.Values[ModuleConstants.Settings.GitHub.AppConnection.Name];
        Assert.DoesNotContain("PRIVATE KEY", saved);

        // another server of the same platform, sharing its keys
        var options = new GitContentOptions { RepositoryPrefix = "web-" };
        var service = GitHubConnections.Service(options, settings, keys);
        var connection = await service.GetConnectionAsync(TestContext.Current.CancellationToken);
        Assert.True(connection.IsEnabled);
        Assert.True(connection.IsConfigured);
        Assert.True(connection.UsesApp);
        Assert.Equal(GitHubConnectionSource.App, connection.Source);
        Assert.Equal("Acme", connection.Organization);
        Assert.Equal("77", connection.AppId);
        Assert.Equal(Pem, connection.PrivateKey);
        Assert.Equal("main", connection.BaseBranch);
        Assert.True(connection.AutoCreateRepositories);
        // the App's organization, with the configuration's naming
        var policy = new GitContentPolicy(service, Options.Create(options), settingsManager: null, storeService: null, new FakeGitContentOwnership());
        Assert.Equal("Acme/web-b2b", (await policy.GetConfiguredRepositoryAsync("b2b", string.Empty, TestContext.Current.CancellationToken))!.Name);
    }

    [Fact]
    public async Task CreateAppAsync_AppOfAnotherOrganization_Throws()
    {
        var settings = new MemorySettingsManager();

        var error = await Assert.ThrowsAsync<GitContentException>(() =>
            Service(settings, github: Conversion("someone-else")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken));

        Assert.Contains("someone-else", error.Message);
        Assert.Empty(settings.Values);
    }

    [Fact]
    public async Task CreateAppAsync_UsedOrExpiredCode_Throws()
    {
        var github = new GitHubScript(("POST", "app-manifests/c0de/conversions", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

        var error = await Assert.ThrowsAsync<GitContentException>(() =>
            Service(new MemorySettingsManager(), github: github).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken));

        Assert.Contains("start the setup again", error.Message);
    }

    [Fact]
    public async Task GetConnectionAsync_CompleteConfigurationAndSavedApp_ReturnsConfiguration()
    {
        var settings = new MemorySettingsManager();
        var keys = new EphemeralDataProtectionProvider();
        await Service(settings, keys, Conversion("Acme")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        var connection = await Service(settings, keys, configure: options =>
        {
            options.Enabled = true;
            options.Organization = "env-org";
            options.Token = "t0ken";
            options.BaseBranch = "develop";
        }).GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GitHubConnectionSource.Configuration, connection.Source);
        Assert.Null(connection.App);
        Assert.Equal("env-org", connection.Organization);
        Assert.False(connection.UsesApp);
    }

    [Fact]
    public async Task GetConnectionAsync_SavedAppOtherServerKeys_LeavesGitOffWithError()
    {
        var settings = new MemorySettingsManager();
        await Service(settings, new EphemeralDataProtectionProvider(), Conversion("Acme")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        // a restored database, or a server whose keys were lost: other keys
        var connection = await Service(settings, new EphemeralDataProtectionProvider()).GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.False(connection.IsConfigured);
        Assert.Null(connection.App);
        Assert.Contains("cannot be read on this server", connection.Error);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    internal static GitHubScript Conversion(string owner) => new(("POST", "app-manifests/c0de/conversions", (_, _) =>
        GitHubScript.Json(new JObject
        {
            ["id"] = 77,
            ["slug"] = "page-builder-acme",
            ["pem"] = Pem,
            ["owner"] = new JObject { ["login"] = owner },
        }.ToString(), HttpStatusCode.Created)));

    internal static GitHubConnectionService Service(MemorySettingsManager settings, IDataProtectionProvider keys = null,
        GitHubScript github = null, Action<GitContentOptions> configure = null, IConfiguration configuration = null)
    {
        var options = new GitContentOptions();
        configure?.Invoke(options);
        return GitHubConnections.Service(options, settings, keys, github, configuration);
    }
}
