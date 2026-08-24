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
    }
}
