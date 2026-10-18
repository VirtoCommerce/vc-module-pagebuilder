using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// The connection in effect: the environment's configuration when complete, else the GitHub App the
    /// one-click setup saved — protected with the platform's keys, and never a reason to fail.
    /// </summary>
    public class GitContentConnectionTests
    {
        private static readonly string Pem = RSA.Create(2048).ExportPkcs8PrivateKeyPem();

        [Fact]
        public void Without_a_saved_App_the_configuration_is_the_connection()
        {
            var connection = Connection(new MemorySettingsManager());

            Assert.False(connection.Value.IsConfigured);
            Assert.False(connection.FromConfiguration);
            Assert.Null(connection.App);
            Assert.Null(connection.Error);
        }

        [Fact]
        public async Task The_setup_keeps_the_App_GitHub_hands_over_and_connects_with_it()
        {
            var settings = new MemorySettingsManager();
            var keys = new EphemeralDataProtectionProvider();
            var github = Conversion("Acme");

            await Connection(settings, keys, github, options => options.PagesRoot = "content")
                .CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

            github.AssertDone();
            var saved = (string)settings.Values[ModuleConstants.Settings.GitHub.AppConnection.Name];
            Assert.DoesNotContain("PRIVATE KEY", saved);

            // another server of the same platform, sharing its keys
            var options = Connection(settings, keys, configure: x => x.PagesRoot = "content").Value;
            Assert.True(options.Enabled);
            Assert.True(options.IsConfigured);
            Assert.True(options.UsesApp);
            Assert.Equal("Acme", options.Organization);
            Assert.Equal("77", options.AppId);
            Assert.Equal(Pem, options.PrivateKey);
            Assert.Equal("main", options.BaseBranch);
            Assert.True(options.AutoCreateRepositories);
            Assert.Equal("content", options.PagesRoot);
            Assert.Equal("Acme/site-b2b", options.RepositoryFor("b2b"));
        }

        [Fact]
        public async Task An_App_made_for_another_organization_is_refused()
        {
            var settings = new MemorySettingsManager();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Connection(settings, github: Conversion("someone-else")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken));

            Assert.Contains("someone-else", error.Message);
            Assert.Empty(settings.Values);
        }

        [Fact]
        public async Task A_used_or_expired_code_is_refused()
        {
            var github = new GitHubScript(("POST", "app-manifests/c0de/conversions", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Connection(new MemorySettingsManager(), github: github).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken));

            Assert.Contains("start the setup again", error.Message);
        }

        [Fact]
        public async Task Configuration_wins_over_a_saved_App()
        {
            var settings = new MemorySettingsManager();
            var keys = new EphemeralDataProtectionProvider();
            await Connection(settings, keys, Conversion("Acme")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

            var connection = Connection(settings, keys, configure: options =>
            {
                options.Enabled = true;
                options.Organization = "env-org";
                options.Token = "t0ken";
                options.BaseBranch = "develop";
            });

            Assert.True(connection.FromConfiguration);
            Assert.Null(connection.App);
            Assert.Equal("env-org", connection.Value.Organization);
            Assert.False(connection.Value.UsesApp);
        }

        [Fact]
        public async Task A_saved_App_this_server_cannot_read_leaves_git_off_and_says_why()
        {
            var settings = new MemorySettingsManager();
            await Connection(settings, new EphemeralDataProtectionProvider(), Conversion("Acme")).CreateAppAsync("c0de", "acme", TestContext.Current.CancellationToken);

            // a restored database, or a server whose keys were lost: other keys
            var connection = Connection(settings, new EphemeralDataProtectionProvider());

            Assert.False(connection.Value.IsConfigured);
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

        internal static GitContentConnection Connection(MemorySettingsManager settings, IDataProtectionProvider keys = null,
            GitHubScript github = null, Action<GitContentOptions> configure = null) =>
            new(new OptionsFactory<GitContentOptions>([new ConfigureNamedOptions<GitContentOptions>(Options.DefaultName, configure ?? (_ => { }))], []),
                settings, keys ?? new EphemeralDataProtectionProvider(), github ?? new GitHubScript(), NullLogger<GitContentConnection>.Instance);
    }
}
