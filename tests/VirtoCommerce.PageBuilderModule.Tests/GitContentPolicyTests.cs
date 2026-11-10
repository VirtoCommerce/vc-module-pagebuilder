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

        private static GitContentOptions PerSite() => new()
        {
            Enabled = true,
            Organization = "VirtoCommerce-Sites",
            AppId = "123",
            PrivateKey = "-----BEGIN PRIVATE KEY-----",
            BaseBranch = "main",
            AllowedRepositories = ["acme/website", "VirtoCommerce-Sites/site-a"],
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
            // Treating it as off would send the store's publications straight to production.
            var policy = Policy(PerSite(), new FakeSettingsManager(value: true, repository: "https://github.com/acme/website"));

            await Assert.ThrowsAsync<GitContentException>(() => policy.IsEnabledForStoreAsync("b2b-store", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<GitContentException>(() => policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("acme/website", "acme/website")]
        [InlineData("acme/website", "ACME/Website")]
        [InlineData("ACME/*", "acme/anything")]
        public async Task GetRepositoryAsync_StoreSettingOnTheAllowList_ReturnsIt(string entry, string setting)
        {
            var options = PerSite();
            options.AllowedRepositories = [entry];

            Assert.Equal(setting, await Policy(options, new FakeSettingsManager(value: true, repository: setting)).GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("other/website")]
        [InlineData("acme/website-2")]
        [InlineData("acme-staging/website")]
        // Being in the organization is not enough: nothing marks an unrelated repository there.
        [InlineData("VirtoCommerce-Sites/internal-tools")]
        public async Task IsEnabledForStoreAsync_StoreSettingNotOnTheAllowList_ThrowsNamingItAndTheOption(string setting)
        {
            var options = PerSite();
            options.AllowedRepositories = ["acme/website", "acme-two/*"];
            var policy = Policy(options, new FakeSettingsManager(value: true, repository: setting));

            var error = await Assert.ThrowsAsync<GitContentException>(() => policy.IsEnabledForStoreAsync("b2b-store", TestContext.Current.CancellationToken));

            Assert.Contains(setting, error.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(GitContentOptions.AllowedRepositories), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetRepositoryAsync_AllowListEmpty_RefusesAnyStoreSetting()
        {
            var options = PerSite();
            options.AllowedRepositories = [];
            var policy = Policy(options, new FakeSettingsManager(value: true, repository: "acme/website"));

            await Assert.ThrowsAsync<GitContentException>(() => policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("owner/repo")]
        [InlineData(" OWNER/Repo ")]
        public async Task GetRepositoryInfoAsync_StoreSettingIsTheSharedRepository_NeedsNoEntry(string setting)
        {
            var repository = await Policy(Configured(), new FakeSettingsManager(value: true, repository: setting)).GetRepositoryInfoAsync("b2b-store", TestContext.Current.CancellationToken);

            Assert.Equal(setting.Trim(), repository!.Name);
            Assert.Equal(GitRepositorySource.StoreSetting, repository.Source);
        }

        [Theory]
        [InlineData("owner/name")]
        [InlineData("Owner-1/name.with_chars-2")]
        [InlineData("owner/*")]
        public void Validate_WellFormedAllowedRepository_Passes(string entry)
        {
            var options = PerSite();
            options.AllowedRepositories = [entry];

            options.Validate();
        }

        [Theory]
        [InlineData("")]
        [InlineData("*")]
        [InlineData("owner")]
        [InlineData("*/name")]
        [InlineData("*/*")]
        [InlineData("owner/na*")]
        [InlineData("owner/..")]
        [InlineData("owner/name/contents")]
        [InlineData("https://github.com/owner/name")]
        public void Validate_MalformedAllowedRepository_ThrowsNamingIt(string entry)
        {
            var options = PerSite();
            options.AllowedRepositories = ["acme/website", entry];

            var error = Assert.Throws<InvalidOperationException>(options.Validate);

            Assert.Contains(nameof(GitContentOptions.AllowedRepositories), error.Message, StringComparison.Ordinal);
            Assert.Contains($"\"{entry}\"", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AllowedRepositories_FromConfiguration_AreBound()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [$"{GitContentOptions.SectionName}:AllowedRepositories:0"] = "acme/website",
                    [$"{GitContentOptions.SectionName}:AllowedRepositories:1"] = "acme-content/*",
                })
                .Build();

            Assert.Equal(["acme/website", "acme-content/*"], configuration.GetSection(GitContentOptions.SectionName).Get<GitContentOptions>()!.AllowedRepositories);
        }

        [Fact]
        public void Validate_MalformedAllowedRepositoryWithEnabledUnset_Throws()
        {
            // The one-click setup connects with Enabled unset, and the list applies to its stores as well.
            var options = new GitContentOptions { AllowedRepositories = ["owner"] };

            Assert.Throws<InvalidOperationException>(options.Validate);
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
            // A null settings manager would throw if the policy consulted it.
            var policy = AppPolicy(storeSettings: null, configuration);

            Assert.False(await policy.IsEnabledForStoreAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        private static GitContentPolicy AppPolicy(ISettingsManager storeSettings, IConfiguration configuration = null)
        {
            var options = new GitContentOptions();
            var connection = GitHubConnections.CreateService(options, new FakeSettingsManager { ReadFailure = new TimeoutException("the database did not answer") },
                configuration: configuration);

            return new GitContentPolicy(connection, Options.Create(options), storeSettings, new FakeGitContentOwnership());
        }

        [Fact]
        public async Task GetRepositoryAsync_RepositoryOfAnotherStore_ThrowsOwnershipException()
        {
            // "B2B Store" and "b2b-store" are different stores that name the same repository.
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
        public async Task GetConfiguredRepositoryAsync_AllowedSettingNotOwnerAndName_Throws(string configured)
        {
            // The setting ends up in API urls and is never replaced by the computed name.
            // An "owner/*" entry lets such a setting through the allow list, so the format check alone has to refuse it.
            var options = PerSite();
            options.AllowedRepositories = [configured];

            await Assert.ThrowsAsync<GitContentException>(() =>
                Policy(options, settings: null).GetConfiguredRepositoryAsync("b2b-store", configured, TestContext.Current.CancellationToken));
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

        [Theory]
        [InlineData("VirtoCommerce-Sites", "virtocommerce-sites/site-a", true)]
        [InlineData("VirtoCommerce-Sites", "acme/site-a", false)]
        [InlineData("VirtoCommerce-Sites", "VirtoCommerce-Sites-Staging/site-a", false)]
        [InlineData(null, "owner/repo", false)]
        public void IsInOrganization_Repository_TrueOnlyForConfiguredOrganization(string organization, string repository, bool expected)
        {
            Assert.Equal(expected, new GitHubConnection { Organization = organization }.IsInOrganization(repository));
        }
    }
}
