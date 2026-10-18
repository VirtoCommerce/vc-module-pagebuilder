using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.StoreModule.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

/// <summary>
/// Site repositories follow their store: a new store gets one when the organization is set to create them,
/// and a deleted store's repository is archived. Both run as a background job — GitHub must not hold up
/// saving a store, and a call that fails is retried.
/// <para>
/// Only with one repository per site (an organization configured): the single repository of earlier
/// installations is shared by every store and belongs to none of them.
/// </para>
/// </summary>
public class GitContentStoreChangedEventHandler(
    IOptions<GitContentOptions> options,
    ILogger<GitContentStoreChangedEventHandler> logger)
    : IEventHandler<StoreChangedEvent>
{
    private static readonly EnqueueOptions _enqueueOptions = new()
    {
        MaxRetryAttempts = GitContentRepositoryJob.MaxRetryAttempts,
    };

    public async Task Handle(StoreChangedEvent message)
    {
        foreach (var payload in JobsFor(message, options.Value))
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

    /// <summary>What the change calls for. The repository of a deleted store is worked out here, while its settings are still on the event.</summary>
    internal static IEnumerable<GitContentRepositoryJobPayload> JobsFor(StoreChangedEvent message, GitContentOptions options)
    {
        if (!options.Enabled || !options.IsConfigured || string.IsNullOrWhiteSpace(options.Organization))
        {
            yield break;
        }

        foreach (var entry in message.ChangedEntries)
        {
            var store = entry.NewEntry ?? entry.OldEntry;
            if (string.IsNullOrEmpty(store?.Id))
            {
                continue;
            }

            if (entry.EntryState == EntryState.Added && options.AutoCreateRepositories)
            {
                yield return new GitContentRepositoryJobPayload { StoreId = store.Id };
            }
            else if (entry.EntryState == EntryState.Deleted)
            {
                var configured = store.Settings?
                    .FirstOrDefault(x => x.Name == ModuleConstants.Settings.StoreLevelSettings.GitContentRepository.Name)?
                    .Value?.ToString();
                var repository = RepositoryOrNull(options, store.Id, configured);

                // a repository the store named itself was somebody's before the store, and stays theirs
                if (options.IsInOrganization(repository))
                {
                    yield return new GitContentRepositoryJobPayload { StoreId = store.Id, ArchiveRepository = repository };
                }
            }
        }
    }

    private static string RepositoryOrNull(GitContentOptions options, string storeId, string configured)
    {
        try
        {
            return options.RepositoryFor(storeId, configured);
        }
        catch (InvalidOperationException)
        {
            // a setting that never named a usable repository has nothing to archive
            return null;
        }
    }
}

public class GitContentRepositoryJobPayload
{
    public string StoreId { get; set; }

    /// <summary>The repository of a deleted store, to archive; empty means "make the store's repository ready and switch it on".</summary>
    public string ArchiveRepository { get; set; }
}

public class GitContentRepositoryJob(IGitContentProvisioner provisioner) : IBackgroundJobHandler<GitContentRepositoryJobPayload>
{
    public const int MaxRetryAttempts = 3;

    public virtual Task Execute(GitContentRepositoryJobPayload payload, IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        string.IsNullOrEmpty(payload.ArchiveRepository)
            ? provisioner.ConnectAsync(payload.StoreId, cancellationToken)
            : provisioner.ArchiveRepositoryAsync(payload.StoreId, payload.ArchiveRepository, cancellationToken);
}
