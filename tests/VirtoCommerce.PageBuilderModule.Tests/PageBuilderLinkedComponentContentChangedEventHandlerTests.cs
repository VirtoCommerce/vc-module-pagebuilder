using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Handlers;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentContentChangedEventHandlerTests
{
    [Fact]
    public void BatchPageIds_KeepsEveryDatabaseAndEventBatchBelowSqlServerParameterLimit()
    {
        var pageIds = Enumerable.Range(0, 1201).Select(x => $"page-{x}").ToArray();

        var batches = PageBuilderLinkedComponentContentPropagationJob
            .BatchPageIds(pageIds)
            .ToArray();

        Assert.Equal([500, 500, 201], batches.Select(x => x.Length));
        Assert.Equal(pageIds.OrderBy(x => x, System.StringComparer.Ordinal), batches.SelectMany(x => x));
    }

    [Fact]
    public async Task Handle_EnqueueFailureDoesNotFailCommittedRequest()
    {
        var handler = new PageBuilderLinkedComponentContentChangedEventHandler(
            new ThrowingBackgroundJobClient(),
            NullLogger<PageBuilderLinkedComponentContentChangedEventHandler>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            handler.Handle(new PageBuilderLinkedComponentContentChangedEvent(["component"])));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PropagationJob_EventFailureEscapesForHangfireRetry()
    {
        var job = new PageBuilderLinkedComponentContentPropagationJob(
            new ReferenceIndexService(["page"]),
            new PageService(),
            new ThrowingEventPublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            job.ProcessAsync(["component"], NoopJobCancellationToken.Instance));

        var retry = typeof(PageBuilderLinkedComponentContentPropagationJob)
            .GetMethod(nameof(PageBuilderLinkedComponentContentPropagationJob.ProcessAsync))!
            .GetCustomAttribute<AutomaticRetryAttribute>();
        Assert.NotNull(retry);
        Assert.Equal(3, retry.Attempts);
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

    private sealed class ThrowingBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) =>
            throw new InvalidOperationException("Simulated Hangfire storage outage.");

        public bool ChangeState(string jobId, IState state, string expectedState) => false;
    }

    private sealed class NoopJobCancellationToken : IJobCancellationToken
    {
        public static readonly NoopJobCancellationToken Instance = new();

        public CancellationToken ShutdownToken => CancellationToken.None;

        public void ThrowIfCancellationRequested()
        {
        }
    }

    private sealed class ReferenceIndexService(IList<string> pageIds)
        : NoopLinkedComponentReferenceIndexService
    {
        public override Task<IList<string>> GetPageIdsAsync(
            IEnumerable<string> linkedComponentIds,
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
