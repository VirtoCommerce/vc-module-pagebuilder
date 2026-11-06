using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.BackgroundJobs;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Handlers;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Events;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

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
    public async Task GetJobPayloadsAsync_AddedStoreWithAutoCreate_ConnectsStore()
    {
        var job = Assert.Single(await GetJobPayloadsAsync(PerSite(), Change(new Store { Id = "s1" }, EntryState.Added)));

        Assert.Equal("s1", job.StoreId);
        Assert.Equal(GitContentRepositoryAction.Connect, job.Action);
        Assert.Null(job.Repository);
    }

    [Fact]
    public async Task GetJobPayloadsAsync_AddedStoreWithoutAutoCreate_ReturnsNoJob()
    {
        Assert.Empty(await GetJobPayloadsAsync(PerSite(autoCreate: false), Change(new Store { Id = "s1" }, EntryState.Added)));
    }

    [Fact]
    public async Task GetJobPayloadsAsync_DeletedStore_ArchivesItsRepository()
    {
        var job = Assert.Single(await GetJobPayloadsAsync(PerSite(), Change(new Store { Id = "s1", Settings = [] }, EntryState.Deleted)));

        Assert.Equal(GitContentRepositoryAction.Archive, job.Action);
        Assert.Equal("acme/site-s1", job.Repository);
    }

    /// <summary>A repository the store named itself was somebody's before the store, and stays theirs.</summary>
    [Fact]
    public async Task GetJobPayloadsAsync_DeletedStoreNamingRepositoryOutsideOrganization_ReturnsNoJob()
    {
        var store = new Store
        {
            Id = "s1",
            Settings =
            [
                new ObjectSettingEntry(ModuleConstants.Settings.StoreLevelSettings.GitContentRepository) { Value = "other/website" },
            ],
        };

        Assert.Empty(await GetJobPayloadsAsync(PerSite(), Change(store, EntryState.Deleted)));
    }

    [Fact]
    public async Task GetJobPayloadsAsync_DeletedStoreNamingRepositoryInOrganization_ReturnsNoJob()
    {
        var store = new Store
        {
            Id = "s1",
            Settings =
            [
                new ObjectSettingEntry(ModuleConstants.Settings.StoreLevelSettings.GitContentRepository) { Value = "acme/website" },
            ],
        };

        Assert.Empty(await GetJobPayloadsAsync(PerSite(), Change(store, EntryState.Deleted)));
    }

    [Fact]
    public async Task GetJobPayloadsAsync_SharedRepository_ReturnsNoJob()
    {
        var shared = new GitContentOptions { Enabled = true, Repository = "acme/content", Token = "t0ken", BaseBranch = "main", AutoCreateRepositories = true };

        Assert.Empty(await GetJobPayloadsAsync(shared, Change(new Store { Id = "s1" }, EntryState.Added), Change(new Store { Id = "s2" }, EntryState.Deleted)));
    }

    /// <summary>Switching a store's history on saves the store: that change must not start a job again.</summary>
    [Fact]
    public async Task GetJobPayloadsAsync_ModifiedStore_ReturnsNoJob()
    {
        Assert.Empty(await GetJobPayloadsAsync(PerSite(), Change(new Store { Id = "s1" }, EntryState.Modified)));
    }

    private static Task<IList<GitContentRepositoryJobPayload>> GetJobPayloadsAsync(GitContentOptions options, params GenericChangedEntry<Store>[] changes)
    {
        var wrapped = Options.Create(options);
        var connection = GitHubConnections.CreateService(options);
        var handler = new TestGitContentStoreChangedEventHandler(connection, new GitContentPolicy(connection, wrapped, settingsManager: null, storeService: null, new FakeGitContentOwnership()));
        return handler.GetJobPayloadsAsync(new StoreChangedEvent(changes));
    }

    private sealed class TestGitContentStoreChangedEventHandler(IGitHubConnectionService gitHubConnectionService, IGitContentPolicy gitContentPolicy)
        : GitContentStoreChangedEventHandler(gitHubConnectionService, gitContentPolicy, NullLogger<GitContentStoreChangedEventHandler>.Instance)
    {
        public new Task<IList<GitContentRepositoryJobPayload>> GetJobPayloadsAsync(StoreChangedEvent message) => base.GetJobPayloadsAsync(message);
    }

    private static GenericChangedEntry<Store> Change(Store store, EntryState state) => new(store, state);
}
