using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Handlers;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Jobs;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderSharedComponentContentChangedEventHandlerTests
{
    [Fact]
    public void BatchPageIds_KeepsEveryDatabaseAndEventBatchBelowSqlServerParameterLimit()
    {
        var pageIds = Enumerable.Range(0, 1201).Select(x => $"page-{x}").ToArray();

        var batches = PageBuilderSharedComponentContentPropagationJob
            .BatchPageIds(pageIds)
            .ToArray();

        Assert.Equal([500, 500, 201], batches.Select(x => x.Length));
        Assert.Equal(pageIds.OrderBy(x => x, System.StringComparer.Ordinal), batches.SelectMany(x => x));
    }

    [Fact]
    public async Task Handle_EnqueueFailureDoesNotFailCommittedRequest()
    {
        UseBackgroundJob(new FakeBackgroundJob(throwOnEnqueue: true));
        var handler = CreateHandler();

        var exception = await Record.ExceptionAsync(() =>
            handler.Handle(new PageBuilderSharedComponentContentChangedEvent(["component"])));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_EnqueuesPropagationWithThreeRetries()
    {
        var backgroundJob = new FakeBackgroundJob();
        UseBackgroundJob(backgroundJob);
        var handler = CreateHandler();

        await handler.Handle(new PageBuilderSharedComponentContentChangedEvent(["component"]));

        var (handlerType, payload, options) = Assert.Single(backgroundJob.Enqueued);
        Assert.Equal(typeof(PageBuilderSharedComponentContentPropagationJob), handlerType);
        Assert.Equal(["component"], Assert.IsType<PageBuilderSharedComponentContentPropagationJobPayload>(payload).SharedComponentIds);
        Assert.Equal(3, options?.MaxRetryAttempts);
    }

    [Fact]
    public async Task PropagationJob_EventFailureEscapesForRetry()
    {
        var job = new PageBuilderSharedComponentContentPropagationJob(
            new ReferenceIndexService(["page"]),
            new PageService(),
            new ThrowingEventPublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            job.ProcessAsync(["component"], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectBoundedAsync_NeverExceedsConfiguredParallelism()
    {
        var active = 0;
        var maximum = 0;

        await PageBuilderEventHandlerBase.SelectBoundedAsync(
            Enumerable.Range(0, 64).ToArray(),
            async _ =>
            {
                var current = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximum, current);
                await Task.Delay(10, TestContext.Current.CancellationToken);
                Interlocked.Decrement(ref active);
                return true;
            });

        Assert.InRange(maximum, 2, PageBuilderEventHandlerBase.MaxDegreeOfParallelism);
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var observed = maximum;
        while (candidate > observed)
        {
            var previous = Interlocked.CompareExchange(ref maximum, candidate, observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }

    private static PageBuilderSharedComponentContentChangedEventHandler CreateHandler() =>
        new(NullLogger<PageBuilderSharedComponentContentChangedEventHandler>.Instance);

    // The handler enqueues through the static BackgroundJob facade, which resolves IBackgroundJob from this provider.
    private static void UseBackgroundJob(IBackgroundJob backgroundJob) =>
        BackgroundJob.Initialize(new ServiceCollection().AddScoped(_ => backgroundJob).BuildServiceProvider());

    private sealed class FakeBackgroundJob(bool throwOnEnqueue = false) : IBackgroundJob
    {
        public List<(Type HandlerType, object Payload, EnqueueOptions Options)> Enqueued { get; } = [];

        public Task<string> Enqueue<THandler>(object payload, EnqueueOptions options = null, CancellationToken cancellationToken = default)
            where THandler : class
        {
            if (throwOnEnqueue)
            {
                throw new InvalidOperationException("Simulated background job storage outage.");
            }

            Enqueued.Add((typeof(THandler), payload, options));
            return Task.FromResult("job-id");
        }
    }

    private sealed class ReferenceIndexService(IList<string> pageIds)
        : NoopSharedComponentReferenceIndexService
    {
        public override Task<IList<string>> GetPageIdsAsync(
            IEnumerable<string> sharedComponentIds,
            CancellationToken cancellationToken = default) => Task.FromResult(pageIds);
    }

    private sealed class PageService : IPageBuilderPageService
    {
        public Task<IList<PageBuilderPage>> GetAsync(
            IList<string> ids,
            string responseGroup = null,
            bool clone = true) => Task.FromResult<IList<PageBuilderPage>>(
                ids.Select(id => new PageBuilderPage
                {
                    Id = id,
                    GroupId = "group",
                    StoreId = "store",
                    Status = "Published",
                }).ToList());

        public Task SaveChangesAsync(IList<PageBuilderPage> models) => Task.CompletedTask;

        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => Task.CompletedTask;
    }

    private sealed class ThrowingEventPublisher : IEventPublisher
    {
        public Task Publish<T>(T @event, CancellationToken cancellationToken = default)
            where T : IEvent => throw new InvalidOperationException("Simulated Pages event failure.");
    }
}
