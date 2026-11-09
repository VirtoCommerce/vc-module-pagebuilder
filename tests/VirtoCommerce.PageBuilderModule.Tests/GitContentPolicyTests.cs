using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.Platform.Core.Settings;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// The git flow must be impossible to enter by accident: it takes the global switch, a usable
    /// connection and the store's opt-in, all three. These tests are the backward-compatibility
    /// guarantee — with the switch off nothing about the module's behaviour may change, and the policy
    /// must not even ask the settings store.
    /// </summary>
    public class GitContentPolicyTests
    {
        private static GitContentOptions Configured(bool enabled = true) => new()
        {
            Enabled = enabled,
            Repository = "owner/repo",
            Token = "t0ken",
            BaseBranch = "master",
        };

        private static GitContentPolicy Policy(GitContentOptions options, ISettingsManager settings, IGitContentOwnership ownership = null) =>
            GitHubConnections.CreatePolicy(Options.Create(options), settings, ownership);

        [Fact]
        public async Task Disabled_globally_never_touches_the_settings_store()
        {
            // a null settings manager would throw if the policy consulted it
            var policy = Policy(Configured(enabled: false), settings: null);

            Assert.False(await policy.IsEnabledForStoreAsync("vccom", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Enabled_but_unconfigured_is_off()
        {
            var options = Configured();
            options.Token = null;

            var policy = Policy(options, settings: null);

            Assert.False(await policy.IsEnabledForStoreAsync("vccom", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Without_a_store_id_is_off()
        {
            var policy = Policy(Configured(), settings: null);

            Assert.False(await policy.IsEnabledForStoreAsync(null, TestContext.Current.CancellationToken));
            Assert.False(await policy.IsEnabledForStoreAsync(string.Empty, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Store_that_did_not_opt_in_is_off()
        {
            var settings = new FakeSettingsManager(value: null);

            var policy = Policy(Configured(), settings);

            Assert.False(await policy.IsEnabledForStoreAsync("vccom", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Store_that_opted_in_is_on()
        {
            var settings = new FakeSettingsManager(value: true);

            Assert.True(await Policy(Configured(), settings).IsEnabledForStoreAsync("vccom", TestContext.Current.CancellationToken));

            Assert.Equal(ModuleConstants.Settings.StoreLevelSettings.GitContentEnabled.Name, settings.AskedName);
            Assert.Equal(ModuleConstants.Settings.StoreSettingsObjectType, settings.AskedObjectType);
            Assert.Equal("vccom", settings.AskedObjectId);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("True", true)]
        [InlineData("false", false)]
        [InlineData("yes", false)]     // not a boolean — never guess "on"
        [InlineData(42, false)]
        public async Task Store_setting_is_read_defensively(object stored, bool expected)
        {
            var policy = Policy(Configured(), new FakeSettingsManager(stored));

            Assert.Equal(expected, await policy.IsEnabledForStoreAsync("vccom", TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Validate_passes_when_disabled_even_if_unconfigured()
        {
            new GitContentOptions { Enabled = false }.Validate();
        }

        [Fact]
        public void Validate_throws_when_enabled_without_a_connection()
        {
            var options = new GitContentOptions { Enabled = true };

            var error = Assert.Throws<InvalidOperationException>(options.Validate);

            Assert.Contains(nameof(GitContentOptions.Repository), error.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(GitContentOptions.Token), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_passes_when_enabled_and_configured()
        {
            Configured().Validate();
        }

        /// <summary>
        /// BaseBranch must have no default. A default boots successfully and merges into a branch the
        /// deploy workflow is not watching, so publishing stops arriving with nothing to see anywhere —
        /// the operator is the only one who knows which branch CI triggers on. Giving the property a
        /// default value would also make <see cref="GitContentOptions.Validate"/>'s check for it
        /// unreachable, which is how this was wrong before.
        /// </summary>
        [Fact]
        public void Validate_reports_a_missing_base_branch()
        {
            var options = Configured();
            options.BaseBranch = null;

            var error = Assert.Throws<InvalidOperationException>(options.Validate);

            Assert.Contains(nameof(GitContentOptions.BaseBranch), error.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// An empty ReleaseBranch is a valid configuration and means one thing only: this installation has
        /// no production promotion. It must not keep the flow from starting, or every deployment without a
        /// second environment would be forced to invent a branch name it has no use for.
        /// </summary>
        [Fact]
        public void Validate_passes_when_promotion_is_disabled()
        {
            var options = Configured();
            options.ReleaseBranch = null;

            options.Validate();
        }

        [Theory]
        [InlineData("master")]
        // Refs differing only in case are the same ref on a case-insensitive host and a trap everywhere
        // else, so this is refused rather than quietly promoting into a branch nobody watches.
        [InlineData("MASTER")]
        [InlineData("  master  ")]
        public void Validate_refuses_a_release_branch_that_is_the_base_branch(string releaseBranch)
        {
            var options = Configured();
            options.ReleaseBranch = releaseBranch;

            var error = Assert.Throws<InvalidOperationException>(options.Validate);

            Assert.Contains(nameof(GitContentOptions.ReleaseBranch), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_passes_when_the_two_branches_differ()
        {
            var options = Configured();
            options.ReleaseBranch = "release";

            options.Validate();
        }

        // ── one repository per site ──────────────────────────────────────────────────────────────────

        private static GitContentOptions PerSite() => new()
        {
            Enabled = true,
            Organization = "VirtoCommerce-Sites",
            AppId = "123",
            PrivateKey = "-----BEGIN PRIVATE KEY-----",
            BaseBranch = "main",
        };

        [Fact]
        public async Task GetRepositoryAsync_Organization_ReturnsStoreRepositoryInOrganization()
        {
            var policy = Policy(PerSite(), new FakeSettingsManager(value: true));

            Assert.Equal("VirtoCommerce-Sites/site-b2b-store", await policy.GetRepositoryAsync("B2B-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetRepositoryAsync_StoreOff_ReturnsNullWhileConfiguredRepositoryIsNamed()
        {
            var policy = Policy(PerSite(), new FakeSettingsManager(value: false));

            Assert.Null(await policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
            Assert.Equal("VirtoCommerce-Sites/site-b2b-store", (await policy.GetConfiguredRepositoryAsync("b2b-store", TestContext.Current.CancellationToken))!.Name);
        }

        [Fact]
        public async Task GetRepositoryAsync_StoreSetting_ReturnsTrimmedSetting()
        {
            var policy = Policy(PerSite(), new FakeSettingsManager(value: true, repository: " acme/website "));

            Assert.Equal("acme/website", await policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetRepositoryAsync_StoreOffWithMalformedSetting_ReturnsNullAndStaysOff()
        {
            var policy = Policy(PerSite(), new FakeSettingsManager(value: false, repository: "https://github.com/acme/website"));

            Assert.Null(await policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
            Assert.False(await policy.IsEnabledForStoreAsync("b2b-store", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<GitContentException>(() => policy.GetConfiguredRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task IsEnabledForStoreAsync_StoreOnWithMalformedSetting_Throws()
        {
            // treating it as off would send the store's publications straight to production
            var policy = Policy(PerSite(), new FakeSettingsManager(value: true, repository: "https://github.com/acme/website"));

            await Assert.ThrowsAsync<GitContentException>(() => policy.IsEnabledForStoreAsync("b2b-store", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<GitContentException>(() => policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task IsEnabledForStoreAsync_SavedAppUnreadableAndStoreOn_Throws()
        {
            var policy = AppPolicy(storeSettings: new FakeSettingsManager(value: true));

            var exception = await Assert.ThrowsAsync<GitContentException>(() => policy.IsEnabledForStoreAsync("b2b-store", TestContext.Current.CancellationToken));
            Assert.Contains("could not be read", exception.Message);
            await Assert.ThrowsAsync<GitContentException>(() => policy.GetRepositoryInfoAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task IsEnabledForStoreAsync_SavedAppUnreadableAndStoreOff_IsOff()
        {
            var policy = AppPolicy(storeSettings: new FakeSettingsManager(value: false));

            Assert.False(await policy.IsEnabledForStoreAsync("b2b-store", TestContext.Current.CancellationToken));
            Assert.Null(await policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task IsEnabledForStoreAsync_SavedAppUnreadableAndSwitchedOff_IsOffWithoutReadingTheStore()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { [$"{GitContentOptions.SectionName}:Enabled"] = "false" })
                .Build();
            // a null settings manager would throw if the policy consulted it
            var policy = AppPolicy(storeSettings: null, configuration);

            Assert.False(await policy.IsEnabledForStoreAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        // the one-click App connection, whose saved setting the database does not return
        private static GitContentPolicy AppPolicy(ISettingsManager storeSettings, IConfiguration configuration = null)
        {
            var options = new GitContentOptions();
            var connection = GitHubConnections.CreateService(options, new FakeSettingsManager { ReadFailure = new TimeoutException("the database did not answer") },
                configuration: configuration);

            return new GitContentPolicy(connection, Options.Create(options), storeSettings, storeService: null, new FakeGitContentOwnership());
        }

        [Fact]
        public async Task GetRepositoryAsync_RepositoryOfAnotherStore_ThrowsOwnershipException()
        {
            // "B2B Store" and "b2b-store" name the same repository
            var policy = Policy(PerSite(), new FakeSettingsManager(value: true), new FakeGitContentOwnership("b2b-store"));

            await Assert.ThrowsAsync<GitRepositoryOwnershipException>(() => policy.GetRepositoryAsync("B2B Store", TestContext.Current.CancellationToken));
            Assert.True(await policy.IsEnabledForStoreAsync("B2B Store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetRepositoryAsync_StoreSettingNamesAnotherStoresRepository_ThrowsOwnershipException()
        {
            var policy = Policy(PerSite(), new FakeSettingsManager(value: true, repository: "VirtoCommerce-Sites/site-a"), new FakeGitContentOwnership("a"));

            await Assert.ThrowsAsync<GitRepositoryOwnershipException>(() => policy.GetRepositoryAsync("b", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetRepositoryAsync_OwnOrUnownedRepository_ReturnsRepository()
        {
            Assert.Equal("VirtoCommerce-Sites/site-b2b-store",
                await Policy(PerSite(), new FakeSettingsManager(value: true), new FakeGitContentOwnership("b2b-store")).GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
            Assert.Equal("VirtoCommerce-Sites/site-b2b-store",
                await Policy(PerSite(), new FakeSettingsManager(value: true), new FakeGitContentOwnership()).GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetRepositoryAsync_RepositoryOutsideOrganization_SkipsOwnershipLookup()
        {
            var ownership = new FakeGitContentOwnership("someone");

            Assert.Equal("acme/website", await Policy(PerSite(), new FakeSettingsManager(value: true, repository: "acme/website"), ownership).GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
            Assert.Equal("owner/repo", await Policy(Configured(), new FakeSettingsManager(value: true), ownership).GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
            Assert.Empty(ownership.Asked);
        }

        [Fact]
        public async Task GetRepositoryInfoAsync_EachSource_ReturnsSource()
        {
            Assert.Equal(GitRepositorySource.Site,
                (await Policy(PerSite(), new FakeSettingsManager(value: true)).GetRepositoryInfoAsync("b2b-store", TestContext.Current.CancellationToken))!.Source);
            Assert.Equal(GitRepositorySource.StoreSetting,
                (await Policy(PerSite(), new FakeSettingsManager(value: true, repository: "acme/website")).GetRepositoryInfoAsync("b2b-store", TestContext.Current.CancellationToken))!.Source);
            Assert.Equal(GitRepositorySource.Shared,
                (await Policy(Configured(), new FakeSettingsManager(value: true)).GetRepositoryInfoAsync("b2b-store", TestContext.Current.CancellationToken))!.Source);
        }

        [Theory]
        [InlineData("B2B Store", "VirtoCommerce-Sites/site-b2b-store")]
        [InlineData("electronics", "VirtoCommerce-Sites/site-electronics")]
        [InlineData("Shop #1", "VirtoCommerce-Sites/site-shop-1")]
        public async Task GetConfiguredRepositoryAsync_NoRepositorySetting_NamesSiteRepositorySafely(string storeId, string expected)
        {
            var repository = await Policy(PerSite(), settings: null).GetConfiguredRepositoryAsync(storeId, repositorySetting: null, TestContext.Current.CancellationToken);

            Assert.Equal(expected, repository!.Name);
        }

        [Fact]
        public async Task GetConfiguredRepositoryAsync_LongStoreId_KeepsWithinGitHubNameLimit()
        {
            var repository = await Policy(PerSite(), settings: null).GetConfiguredRepositoryAsync(new string('a', 200), repositorySetting: null, TestContext.Current.CancellationToken);

            Assert.Equal(GitContentPolicy.MaxRepositoryNameLength, repository!.Name.Split('/')[1].Length);
        }

        [Theory]
        [InlineData("../orgs/x")]
        [InlineData("acme")]
        [InlineData("acme/..")]
        [InlineData("acme/web site")]
        [InlineData("acme/website/contents")]
        public async Task GetConfiguredRepositoryAsync_SettingNotOwnerAndName_Throws(string configured)
        {
            // the setting ends up in API urls, and is never replaced by the computed name
            await Assert.ThrowsAsync<GitContentException>(() =>
                Policy(PerSite(), settings: null).GetConfiguredRepositoryAsync("b2b-store", configured, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetConfiguredRepositoryAsync_NoOrganization_ReturnsSharedRepository()
        {
            var repository = await Policy(Configured(), settings: null).GetConfiguredRepositoryAsync("anything", repositorySetting: null, TestContext.Current.CancellationToken);

            Assert.Equal("owner/repo", repository!.Name);
        }

        [Fact]
        public void Validate_SharedRepositoryAndOrganization_Throws()
        {
            var options = PerSite();
            options.Repository = "owner/repo";

            var error = Assert.Throws<InvalidOperationException>(options.Validate);

            Assert.Contains(nameof(GitContentOptions.Organization), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_OrganizationWithGitHubApp_Passes()
        {
            PerSite().Validate();
            Assert.True(PerSite().IsConfigured);
        }

        [Fact]
        public async Task IsInOrganizationAsync_Repository_TrueOnlyForConfiguredOrganization()
        {
            var ct = TestContext.Current.CancellationToken;

            Assert.True(await Policy(PerSite(), settings: null).IsInOrganizationAsync("virtocommerce-sites/site-a", ct));
            Assert.False(await Policy(PerSite(), settings: null).IsInOrganizationAsync("acme/site-a", ct));
            Assert.False(await Policy(PerSite(), settings: null).IsInOrganizationAsync("VirtoCommerce-Sites-Staging/site-a", ct));
            Assert.False(await Policy(Configured(), settings: null).IsInOrganizationAsync("owner/repo", ct));
        }
    }
}
