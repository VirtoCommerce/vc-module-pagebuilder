using System;
using System.Threading.Tasks;
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

        private static GitContentPolicy Policy(GitContentOptions options, ISettingsManager settings) =>
            new(Options.Create(options), settings);

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
        public async Task Per_site_a_store_gets_its_own_repository_in_the_organization()
        {
            var policy = Policy(PerSite(), new FakeSettingsManager(value: true));

            Assert.Equal("VirtoCommerce-Sites/site-b2b-store", await policy.GetRepositoryAsync("B2B-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Per_site_a_store_that_is_off_has_no_repository_to_use_but_one_to_switch_on_with()
        {
            var policy = Policy(PerSite(), new FakeSettingsManager(value: false));

            Assert.Null(await policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
            Assert.Equal("VirtoCommerce-Sites/site-b2b-store", await policy.ResolveRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task A_store_that_names_its_own_repository_uses_it()
        {
            var policy = Policy(PerSite(), new FakeSettingsManager(value: true, repository: " acme/website "));

            Assert.Equal("acme/website", await policy.GetRepositoryAsync("b2b-store", TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("B2B Store", "VirtoCommerce-Sites/site-b2b-store")]
        [InlineData("electronics", "VirtoCommerce-Sites/site-electronics")]
        [InlineData("Shop #1", "VirtoCommerce-Sites/site-shop-1")]
        public void RepositoryFor_names_the_site_repository_safely(string storeId, string expected)
        {
            Assert.Equal(expected, PerSite().RepositoryFor(storeId));
        }

        [Fact]
        public void RepositoryFor_keeps_within_the_GitHub_name_limit()
        {
            var name = PerSite().RepositoryFor(new string('a', 200)).Split('/')[1];

            Assert.Equal(GitContentOptions.MaxRepositoryNameLength, name.Length);
        }

        /// <summary>
        /// The store's own setting ends up in api urls, so anything but a plain "owner/name" is refused — never
        /// quietly replaced by the computed name, which would put the store's pages somewhere it did not ask for.
        /// </summary>
        [Theory]
        [InlineData("../orgs/x")]
        [InlineData("acme")]
        [InlineData("acme/..")]
        [InlineData("acme/web site")]
        [InlineData("acme/website/contents")]
        public void RepositoryFor_refuses_a_setting_that_is_not_owner_and_name(string configured)
        {
            Assert.Throws<InvalidOperationException>(() => PerSite().RepositoryFor("b2b-store", configured));
        }

        [Fact]
        public void RepositoryFor_without_an_organization_is_the_shared_repository()
        {
            Assert.Equal("owner/repo", Configured().RepositoryFor("anything"));
        }

        [Fact]
        public void Validate_refuses_the_shared_repository_and_an_organization_together()
        {
            var options = PerSite();
            options.Repository = "owner/repo";

            var error = Assert.Throws<InvalidOperationException>(options.Validate);

            Assert.Contains(nameof(GitContentOptions.Organization), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_passes_with_an_organization_and_a_GitHub_App()
        {
            PerSite().Validate();
            Assert.True(PerSite().IsConfigured);
        }

        [Fact]
        public void IsInOrganization_is_only_the_configured_organization()
        {
            Assert.True(PerSite().IsInOrganization("virtocommerce-sites/site-a"));
            Assert.False(PerSite().IsInOrganization("acme/site-a"));
            Assert.False(PerSite().IsInOrganization("VirtoCommerce-Sites-Staging/site-a"));
            Assert.False(Configured().IsInOrganization("owner/repo"));
        }
    }
}
