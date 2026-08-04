using Hangfire;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public class PageBuilderSharedComponentContentChangedEventHandler(
    IBackgroundJobClient backgroundJobClient,
    ILogger<PageBuilderSharedComponentContentChangedEventHandler> logger)
    : IEventHandler<PageBuilderSharedComponentContentChangedEvent>
{
    public Task Handle(PageBuilderSharedComponentContentChangedEvent message)
    {
        var sharedComponentIds = message.SharedComponentIds.ToArray();
        if (sharedComponentIds.Length == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            backgroundJobClient.Enqueue<PageBuilderSharedComponentContentPropagationJob>(
                job => job.ProcessAsync(sharedComponentIds, JobCancellationToken.Null));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to enqueue Shared Component propagation for {SharedComponentIds}",
                sharedComponentIds);
        }

        return Task.CompletedTask;
    }
}
