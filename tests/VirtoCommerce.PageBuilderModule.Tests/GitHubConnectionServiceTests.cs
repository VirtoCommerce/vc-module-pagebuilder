using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Tests.GitHubConnections;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GitHubConnectionServiceTests
{
    [Fact]
    public async Task GetConnectionAsync_NoSavedApp_ReturnsConfiguration()
    {
        var connection = await CreateService(new FakeSettingsManager()).GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.False(connection.IsConfigured);
        Assert.Null(connection.Source);
        Assert.Null(connection.App);
        Assert.Null(connection.Error);
    }

    [Fact]
    public async Task GetConnectionAsync_EnabledFalseInConfiguration_SwitchesSavedAppOff()
    {
        var settings = new FakeSettingsManager();
        var keys = new EphemeralDataProtectionProvider();
        await CreateService(settings, keys, CreateManifestConversion("Acme")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [$"{GitContentOptions.SectionName}:Enabled"] = "false" })
            .Build();

        var connection = await CreateService(settings, keys, configuration: configuration, configure: x => x.Enabled = false)
            .GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.True(connection.UsesApp);
        Assert.False(connection.IsEnabled);
    }

    [Fact]
    public async Task CreateAppAsync_CodeOfTheOrganization_SavesTheAppEncryptedAndConnectsWithIt()
    {
        var settings = new FakeSettingsManager();
        var keys = new EphemeralDataProtectionProvider();
        var github = CreateManifestConversion("Acme");

        await CreateService(settings, keys, github).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        github.AssertDone();
        var saved = (string)settings.Values[ModuleConstants.Settings.GitHub.AppConnection.Name];
        Assert.DoesNotContain("PRIVATE KEY", saved);

        var options = new GitContentOptions { RepositoryPrefix = "web-" };
        var service = CreateService(options, settings, keys);
        var connection = await service.GetConnectionAsync(TestContext.Current.CancellationToken);
        Assert.True(connection.IsEnabled);
        Assert.True(connection.IsConfigured);
        Assert.True(connection.UsesApp);
        Assert.Equal(GitHubConnectionSource.App, connection.Source);
        Assert.Equal("Acme", connection.Organization);
        Assert.Equal("77", connection.AppId);
        Assert.Equal(AppPrivateKey, connection.PrivateKey);
        Assert.Equal("main", connection.BaseBranch);
        Assert.True(connection.AutoCreateRepositories);
        var policy = new GitContentPolicy(service, Options.Create(options), settingsManager: null, storeService: null, new FakeGitContentOwnership());
        Assert.Equal("Acme/web-b2b", (await policy.GetConfiguredRepositoryAsync("b2b", string.Empty, TestContext.Current.CancellationToken))!.Name);
    }

    [Fact]
    public async Task CreateAppAsync_AppOfAnotherOrganization_Throws()
    {
        var settings = new FakeSettingsManager();

        var error = await Assert.ThrowsAsync<GitContentException>(() =>
            CreateService(settings, github: CreateManifestConversion("someone-else")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken));

        Assert.Contains("someone-else", error.Message);
        Assert.Empty(settings.Values);
    }

    [Fact]
    public async Task CreateAppAsync_UsedOrExpiredCode_Throws()
    {
        var github = new GitHubScript(("POST", "app-manifests/c0de/conversions", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

        var error = await Assert.ThrowsAsync<GitContentException>(() =>
            CreateService(new FakeSettingsManager(), github: github).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken));

        Assert.Contains("start the setup again", error.Message);
    }

    [Fact]
    public async Task GetConnectionAsync_CompleteConfigurationAndSavedApp_ReturnsConfiguration()
    {
        var settings = new FakeSettingsManager();
        var keys = new EphemeralDataProtectionProvider();
        await CreateService(settings, keys, CreateManifestConversion("Acme")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        var connection = await CreateService(settings, keys, configure: options =>
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
    public async Task GetConnectionAsync_SavedAppOtherServerKeys_ReportsErrorWithoutApp()
    {
        var settings = new FakeSettingsManager();
        await CreateService(settings, new EphemeralDataProtectionProvider(), CreateManifestConversion("Acme")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

        var connection = await CreateService(settings, new EphemeralDataProtectionProvider()).GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.False(connection.IsConfigured);
        Assert.True(connection.IsEnabled);
        Assert.Null(connection.App);
        Assert.Contains("cannot be read on this server", connection.Error);
    }

    [Fact]
    public async Task GetConnectionAsync_SettingsUnreadable_ReportsErrorWithoutApp()
    {
        var settings = new FakeSettingsManager { ReadFailure = new TimeoutException("the database did not answer") };

        var connection = await CreateService(settings).GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.False(connection.IsConfigured);
        Assert.True(connection.IsEnabled);
        Assert.Null(connection.App);
        Assert.Contains("could not be read", connection.Error);
    }

    [Fact]
    public async Task GetConnectionAsync_SettingsUnreadableAndEnabledFalseInConfiguration_StaysSwitchedOff()
    {
        var settings = new FakeSettingsManager { ReadFailure = new TimeoutException("the database did not answer") };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [$"{GitContentOptions.SectionName}:Enabled"] = "false" })
            .Build();

        var connection = await CreateService(settings, configuration: configuration).GetConnectionAsync(TestContext.Current.CancellationToken);

        Assert.False(connection.IsEnabled);
        Assert.NotNull(connection.Error);
    }
}
