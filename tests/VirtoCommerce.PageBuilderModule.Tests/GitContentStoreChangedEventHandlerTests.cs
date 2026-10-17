using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Handlers;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Events;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>Which store changes reach GitHub: a new store with auto-create on, and a deleted store's own repository.</summary>
    public class GitContentStoreChangedEventHandlerTests
    {
        private static GitContentOptions PerSite(bool autoCreate = true) => new()
        {
            Enabled = true,
            Organization = "acme",
            Token = "t0ken",
            BaseBranch = "main",
            AutoCreateRepositories = autoCreate,
        };

        [Fact]
        public void A_new_store_gets_its_repository_when_the_organization_creates_them()
        {
            var job = Assert.Single(Jobs(PerSite(), Change(new Store { Id = "s1" }, EntryState.Added)));

            Assert.Equal("s1", job.StoreId);
            Assert.Null(job.ArchiveRepository);
        }

        [Fact]
        public void A_new_store_waits_to_be_switched_on_when_the_organization_does_not_create_them()
        {
            Assert.Empty(Jobs(PerSite(autoCreate: false), Change(new Store { Id = "s1" }, EntryState.Added)));
        }

        [Fact]
        public void A_deleted_store_has_its_repository_archived()
        {
            var job = Assert.Single(Jobs(PerSite(), Change(new Store { Id = "s1", Settings = [] }, EntryState.Deleted)));

            Assert.Equal("acme/site-s1", job.ArchiveRepository);
        }

        /// <summary>A repository the store named itself was somebody's before the store, and stays theirs.</summary>
        [Fact]
        public void A_deleted_store_leaves_a_repository_it_named_outside_the_organization_alone()
        {
            var store = new Store
            {
                Id = "s1",
                Settings =
                [
                    new ObjectSettingEntry(ModuleConstants.Settings.StoreLevelSettings.GitContentRepository) { Value = "other/website" },
                ],
            };

            Assert.Empty(Jobs(PerSite(), Change(store, EntryState.Deleted)));
        }

        [Fact]
        public void The_shared_repository_of_earlier_installations_is_nobodys_to_create_or_archive()
        {
            var shared = new GitContentOptions { Enabled = true, Repository = "acme/content", Token = "t0ken", BaseBranch = "main", AutoCreateRepositories = true };

            Assert.Empty(Jobs(shared, Change(new Store { Id = "s1" }, EntryState.Added), Change(new Store { Id = "s2" }, EntryState.Deleted)));
        }

        [Fact]
        public void An_edited_store_reaches_GitHub_not_at_all()
        {
            Assert.Empty(Jobs(PerSite(), Change(new Store { Id = "s1" }, EntryState.Modified)));
        }

        private static List<GitContentRepositoryJobPayload> Jobs(GitContentOptions options, params GenericChangedEntry<Store>[] changes) =>
            GitContentStoreChangedEventHandler.JobsFor(new StoreChangedEvent(changes), options).ToList();

        private static GenericChangedEntry<Store> Change(Store store, EntryState state) => new(store, state);
    }
}
