using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.BackgroundJobs;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Handlers;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Events;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

[Collection(StaticBackgroundJobCollection.Name)]
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

    [Fact]
    public async Task GetJobPayloadsAsync_ModifiedStore_ReturnsNoJob()
    {
        // Switching git on for a store saves the store, and that save must queue no job.
        Assert.Empty(await GetJobPayloadsAsync(PerSite(), Change(new Store { Id = "s1" }, EntryState.Modified)));
    }

    [Fact]
    public async Task Handle_AddedStore_EnqueuesItsJobWithItsRetries()
    {
        var jobs = new RecordingBackgroundJob();
        UseBackgroundJob(jobs);

        await Handler(PerSite()).Handle(new StoreChangedEvent([Change(new Store { Id = "s1" }, EntryState.Added)]));

        var (handler, payload, options) = Assert.Single(jobs.Enqueued);
        Assert.Equal(typeof(GitContentRepositoryJob), handler);
        Assert.Equal("s1", Assert.IsType<GitContentRepositoryJobPayload>(payload).StoreId);
        Assert.Equal(GitContentRepositoryJob.MaxRetryAttempts, options.MaxRetryAttempts);
    }

    [Fact]
    public async Task Handle_JobStorageFails_LogsAndGoesOnWithTheNextStore()
    {
        // The store is saved already, so a job that cannot be queued must not fail its event.
        var jobs = new RecordingBackgroundJob(failFor: "s1");
        UseBackgroundJob(jobs);

        await Handler(PerSite()).Handle(new StoreChangedEvent([Change(new Store { Id = "s1" }, EntryState.Added), Change(new Store { Id = "s2" }, EntryState.Added)]));

        Assert.Equal(["s2"], jobs.Enqueued.Select(x => ((GitContentRepositoryJobPayload)x.Payload).StoreId));
    }

    [Theory]
    [InlineData(GitContentRepositoryAction.Connect, "connect s1")]
    [InlineData(GitContentRepositoryAction.Archive, "archive s1 acme/site-s1")]
    public async Task Job_Action_CallsTheProvisionerForIt(GitContentRepositoryAction action, string call)
    {
        var provisioner = new RecordingProvisioner();
        var payload = new GitContentRepositoryJobPayload { StoreId = "s1", Action = action, Repository = "acme/site-s1" };

        await new GitContentRepositoryJob(provisioner).Execute(payload, context: null, TestContext.Current.CancellationToken);

        Assert.Equal([call], provisioner.Calls);
    }

    private static GitContentStoreChangedEventHandler Handler(GitContentOptions options)
    {
        var connection = GitHubConnections.CreateService(options);
        return new GitContentStoreChangedEventHandler(connection, new GitContentPolicy(connection, Options.Create(options), settingsManager: null, storeService: null,
            new FakeGitContentOwnership()), NullLogger<GitContentStoreChangedEventHandler>.Instance);
    }

    private static void UseBackgroundJob(IBackgroundJob backgroundJob) =>
        BackgroundJob.Initialize(new ServiceCollection().AddScoped(_ => backgroundJob).BuildServiceProvider());

    private sealed class RecordingBackgroundJob(string failFor = null) : IBackgroundJob
    {
        public List<(Type HandlerType, object Payload, EnqueueOptions Options)> Enqueued { get; } = [];

        public Task<string> Enqueue<THandler>(object payload, EnqueueOptions options = null, CancellationToken cancellationToken = default)
            where THandler : class
        {
            if (((GitContentRepositoryJobPayload)payload).StoreId == failFor)
            {
                throw new InvalidOperationException("The job storage does not answer.");
            }

            Enqueued.Add((typeof(THandler), payload, options));
            return Task.FromResult("job-id");
        }
    }

    private sealed class RecordingProvisioner : IGitContentProvisioner
    {
        public List<string> Calls { get; } = [];

        public Task ConnectAsync(string storeId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"connect {storeId}");
            return Task.CompletedTask;
        }

        public Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default)
        {
            Calls.Add($"archive {storeId} {repository}");
            return Task.CompletedTask;
        }

        public Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task CheckOrganizationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
