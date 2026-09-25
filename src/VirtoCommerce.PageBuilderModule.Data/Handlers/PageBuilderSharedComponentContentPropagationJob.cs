using Hangfire;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Handlers;

public class PageBuilderSharedComponentContentPropagationJob(
    IPageBuilderSharedComponentReferenceIndexService referenceIndexService,
    IPageBuilderPageService pageService,
    IEventPublisher eventPublisher)
{
    internal const int PageBatchSize = 500;

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessAsync(
        string[] sharedComponentIds,
        IJobCancellationToken jobCancellationToken)
    {
        jobCancellationToken.ThrowIfCancellationRequested();
        var pageIds = await referenceIndexService.GetPageIdsAsync(
            sharedComponentIds,
            jobCancellationToken.ShutdownToken);
        if (pageIds.Count == 0)
        {
            return;
        }

        // Page saves publish their own event, covering references that move around this snapshot.
        foreach (var pageIdBatch in BatchPageIds(pageIds))
        {
            jobCancellationToken.ThrowIfCancellationRequested();

            foreach (var pageId in pageIdBatch)
            {
                GenericCachingRegion<PageBuilderPage>.ExpireTokenForKey(pageId);
            }

            GenericSearchCachingRegion<PageBuilderPage>.ExpireRegion();

            var pages = await pageService.GetAsync(pageIdBatch);
            var changedEntries = pages
                .Select(x => new GenericChangedEntry<PageBuilderPage>(x, EntryState.Modified))
                .ToArray();

            if (changedEntries.Length > 0)
            {
                await eventPublisher.Publish(
                    new PageBuilderPageChangedEvent(changedEntries),
                    jobCancellationToken.ShutdownToken);
            }
        }
    }

    internal static IEnumerable<string[]> BatchPageIds(IEnumerable<string> pageIds)
    {
        return pageIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x, StringComparer.Ordinal)
            .Chunk(PageBatchSize);
    }
}
