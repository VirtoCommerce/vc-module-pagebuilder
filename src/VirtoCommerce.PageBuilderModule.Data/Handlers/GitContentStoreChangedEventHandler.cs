using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.BackgroundJobs;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

/// <summary>Creates a repository for a new store and archives the repository of a deleted store.</summary>
public class GitContentStoreChangedEventHandler(
    IGitHubConnectionService gitHubConnectionService,
    IGitContentPolicy gitContentPolicy,
    ILogger<GitContentStoreChangedEventHandler> logger)
    : IEventHandler<StoreChangedEvent>
{
    private static readonly EnqueueOptions _enqueueOptions = new()
    {
        MaxRetryAttempts = GitContentRepositoryJob.MaxRetryAttempts,
    };

    public virtual async Task Handle(StoreChangedEvent message)
    {
        foreach (var payload in await GetJobPayloadsAsync(message))
        {
            try
            {
                // The static facade, not an injected IBackgroundJob: RegisterEventHandler resolves this handler once from
                // the root provider and holds it for the process lifetime, so it must not capture a Scoped dependency.
                await BackgroundJob.Enqueue<GitContentRepositoryJob>(payload, _enqueueOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue the Git repository job for store {StoreId}", payload.StoreId);
            }
        }
    }

    /// <summary>Gets the jobs for the store changes. Edited stores get none.</summary>
    protected virtual async Task<IList<GitContentRepositoryJobPayload>> GetJobPayloadsAsync(StoreChangedEvent message)
    {
        var payloads = new List<GitContentRepositoryJobPayload>();

        var connection = await gitHubConnectionService.GetConnectionAsync();
        if (!connection.IsEnabled || !connection.IsConfigured || string.IsNullOrWhiteSpace(connection.Organization))
        {
            return payloads;
        }

        foreach (var entry in message.ChangedEntries)
        {
            var store = entry.NewEntry ?? entry.OldEntry;
            if (string.IsNullOrEmpty(store?.Id))
            {
                continue;
            }

            if (entry.EntryState == EntryState.Added && connection.AutoCreateRepositories)
            {
                payloads.Add(CreatePayload(store.Id, GitContentRepositoryAction.Connect, repository: null));
            }
            else if (entry.EntryState == EntryState.Deleted)
            {
                // a deleted store's settings exist only on the event
                var repositorySetting = store.Settings?.GetValue<string>(ModuleConstants.Settings.StoreLevelSettings.GitContentRepository);
                var repository = await GetRepositoryOrDefaultAsync(store.Id, repositorySetting);

                // never archive a repository named in the store setting
                if (repository?.Source == GitRepositorySource.Site)
                {
                    payloads.Add(CreatePayload(store.Id, GitContentRepositoryAction.Archive, repository.Name));
                }
            }
        }

        return payloads;
    }

    protected virtual async Task<StoreRepository> GetRepositoryOrDefaultAsync(string storeId, string repositorySetting)
    {
        try
        {
            return await gitContentPolicy.GetConfiguredRepositoryAsync(storeId, repositorySetting);
        }
        catch (GitContentException)
        {
            // a setting that never named a usable repository has nothing to archive
            return null;
        }
    }

    private static GitContentRepositoryJobPayload CreatePayload(string storeId, GitContentRepositoryAction action, string repository)
    {
        var payload = AbstractTypeFactory<GitContentRepositoryJobPayload>.TryCreateInstance();
        payload.StoreId = storeId;
        payload.Action = action;
        payload.Repository = repository;
        return payload;
    }
}
