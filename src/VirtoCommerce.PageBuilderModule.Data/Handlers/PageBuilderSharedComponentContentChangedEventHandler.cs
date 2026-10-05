using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Jobs;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public class PageBuilderSharedComponentContentChangedEventHandler(
    ILogger<PageBuilderSharedComponentContentChangedEventHandler> logger)
    : IEventHandler<PageBuilderSharedComponentContentChangedEvent>
{
    private static readonly EnqueueOptions _enqueueOptions = new()
    {
        MaxRetryAttempts = PageBuilderSharedComponentContentPropagationJob.MaxRetryAttempts,
    };

    public async Task Handle(PageBuilderSharedComponentContentChangedEvent message)
    {
        var sharedComponentIds = message.SharedComponentIds.ToArray();
        if (sharedComponentIds.Length == 0)
        {
            return;
        }

        try
        {
            var payload = AbstractTypeFactory<PageBuilderSharedComponentContentPropagationJobPayload>.TryCreateInstance();
            payload.SharedComponentIds = sharedComponentIds;

            // The static facade, not an injected IBackgroundJob: RegisterEventHandler resolves this handler once from
            // the root provider and holds it for the process lifetime, so it must not capture a Scoped dependency.
            await BackgroundJob.Enqueue<PageBuilderSharedComponentContentPropagationJob>(payload, _enqueueOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to enqueue Shared Component propagation for {SharedComponentIds}",
                sharedComponentIds);
        }
    }
}
