using Hangfire;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public class PageBuilderLinkedComponentContentChangedEventHandler(
    IBackgroundJobClient backgroundJobClient,
    ILogger<PageBuilderLinkedComponentContentChangedEventHandler> logger)
    : IEventHandler<PageBuilderLinkedComponentContentChangedEvent>
{
    public Task Handle(PageBuilderLinkedComponentContentChangedEvent message)
    {
        var linkedComponentIds = message.LinkedComponentIds.ToArray();
        if (linkedComponentIds.Length == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            backgroundJobClient.Enqueue<PageBuilderLinkedComponentContentPropagationJob>(
                job => job.ProcessAsync(linkedComponentIds, JobCancellationToken.Null));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to enqueue Shared Component propagation for {LinkedComponentIds}",
                linkedComponentIds);
        }

        return Task.CompletedTask;
    }
}
