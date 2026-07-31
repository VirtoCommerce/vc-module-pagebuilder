using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentAggregatePersistenceTests
{
    [Fact]
    public async Task SaveWithContentAsync_PersistsAggregateAndPublishesEventsAfterCommit()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);
        var model = new PageBuilderLinkedComponent
        {
            StoreId = StoreId,
            Name = " Shared hero ",
        };

        await service.SaveWithContentAsync(
            model,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(model.Id));
        await using var context = database.CreateContext();
        var component = await context.Set<PageBuilderLinkedComponentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReference = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(model.Id, component.Id);
        Assert.Equal("Shared hero", component.Name);
        Assert.Equal(model.Id, content.Id);
        Assert.Equal(ContentWithAsset(NewAssetUrl), content.ComponentContent);
        Assert.Equal(NewAssetUrl, assetReference.NormalizedAssetUrl);
        Assert.Equal(
            [
                typeof(PageBuilderLinkedComponentChangingEvent),
                typeof(PageBuilderLinkedComponentChangedEvent),
                typeof(PageBuilderLinkedComponentContentChangedEvent),
            ],
            events.EventTypes);
    }

    [Fact]
    public async Task SaveWithContentAsync_CommitFailureRollsBackMetadataContentAndAssetReferences()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.FailComponentAssetReferenceInsertsAsync();
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);
        var model = new PageBuilderLinkedComponent
        {
            StoreId = StoreId,
            Name = "Shared hero",
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => service.SaveWithContentAsync(
            model,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken));

        await using var context = database.CreateContext();
        Assert.Empty(await context.Set<PageBuilderLinkedComponentEntity>()
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.Set<PageBuilderLinkedComponentContentEntity>()
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal([typeof(PageBuilderLinkedComponentChangingEvent)], events.EventTypes);
    }

    [Fact]
    public async Task SaveContentAsync_CommitFailureKeepsPreviousContentAndExactAssetIndex()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        await database.SeedReferencingPagesAsync();
        await database.FailComponentAssetReferenceInsertsAsync();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentContentService(database.RepositoryFactory, events);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.SaveContentAsync(
            ComponentId,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken));

        await using var context = database.CreateContext();
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var referencingPageModifiedDate = await context.Set<PageBuilderPageEntity>()
            .Where(x => x.Id == ReferencingPageId)
            .Select(x => x.ModifiedDate)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(OldAssetUrl), content.ComponentContent);
        Assert.Equal([OldAssetUrl], assetReferences);
        Assert.Equal(OldPageModifiedDate, referencingPageModifiedDate);
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task SaveContentAsync_EventDeliveryFailureDoesNotFailCommittedRequestAndLeavesRecoveryMarker()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        await database.SeedReferencingPagesAsync();
        var service = new PageBuilderLinkedComponentContentService(
            database.RepositoryFactory,
            new ThrowingContentChangedEventPublisher());

        await service.SaveContentAsync(
            ComponentId,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);

        await AssertContentAndRecoveryMarkerWereCommittedAsync(database);
    }

    [Fact]
    public async Task SaveWithContentAsync_EventDeliveryFailureDoesNotFailCommittedRequestAndLeavesRecoveryMarker()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        await database.SeedReferencingPagesAsync();
        using var cache = new TestPlatformMemoryCache();
        var service = new PageBuilderLinkedComponentService(
            database.RepositoryFactory,
            cache,
            new ThrowingContentChangedEventPublisher());

        await service.SaveWithContentAsync(
            new PageBuilderLinkedComponent
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Renamed shared hero",
            },
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);

        await AssertContentAndRecoveryMarkerWereCommittedAsync(database);
    }

    [Fact]
    public async Task TrySaveContentAsync_WhenIdentityMatches_ReplacesComponentAssetIndexExactly()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var expectedComponent = await database.LoadComponentAsync();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentContentService(database.RepositoryFactory, events);

        Assert.Equal(
            ContentWithAsset(OldAssetUrl),
            await service.TryLoadContentAsync(expectedComponent, TestContext.Current.CancellationToken));

        var saved = await service.TrySaveContentAsync(
            expectedComponent,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);

        Assert.True(saved);
        await using var context = database.CreateContext();
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal([NewAssetUrl], assetReferences);
        Assert.Equal([typeof(PageBuilderLinkedComponentContentChangedEvent)], events.EventTypes);
    }

    [Fact]
    public async Task TryLoadContentAsync_WhenStoreIdDiffersOnlyByCase_ReturnsAuthorizedContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var expectedComponent = await database.LoadComponentAsync();
        expectedComponent.StoreId = StoreId.ToUpperInvariant();
        var service = new PageBuilderLinkedComponentContentService(
            database.RepositoryFactory,
            new RecordingEventPublisher());

        var content = await service.TryLoadContentAsync(
            expectedComponent,
            TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(OldAssetUrl), content);
    }

    [Fact]
    public async Task SaveContentAsync_WhenAssetIsStillUsed_KeepsSingleExactReference()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentContentService(database.RepositoryFactory, events);

        await service.SaveContentAsync(
            ComponentId,
            $"{{ \"settings\": {{ \"image\": \"{OldAssetUrl}\", \"alt\": \"Updated\" }}, \"content\": [] }}",
            TestContext.Current.CancellationToken);

        await using var context = database.CreateContext();
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal([OldAssetUrl], assetReferences);
        Assert.Equal([typeof(PageBuilderLinkedComponentContentChangedEvent)], events.EventTypes);
    }

    [Fact]
    public async Task SaveContentAsync_ConcurrentWritersKeepContentAndAssetIndexOnSameVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentWithoutAssetsAsync();
        var firstWriterHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriterHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstService = new PageBuilderLinkedComponentContentService(
            () => new CoordinatedPageBuilderModuleRepository(
                database.CreateContext(),
                async cancellationToken =>
                {
                    firstWriterHasLock.SetResult();
                    await releaseFirstWriter.Task.WaitAsync(cancellationToken);
                }),
            new RecordingEventPublisher());
        var secondService = new PageBuilderLinkedComponentContentService(
            () => new CoordinatedPageBuilderModuleRepository(
                database.CreateContext(),
                _ =>
                {
                    secondWriterHasLock.SetResult();
                    return Task.CompletedTask;
                }),
            new RecordingEventPublisher());

        var firstWrite = firstService.SaveContentAsync(
            ComponentId,
            ContentWithAsset(OldAssetUrl),
            TestContext.Current.CancellationToken);
        await firstWriterHasLock.Task.WaitAsync(TestContext.Current.CancellationToken);

        var secondWrite = Task.Run(
            async () =>
            {
                secondWriterStarted.SetResult();
                await secondService.SaveContentAsync(
                    ComponentId,
                    ContentWithAsset(NewAssetUrl),
                    TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken);
        await secondWriterStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(secondWriterHasLock.Task.IsCompleted);

        releaseFirstWriter.SetResult();
        await Task.WhenAll(firstWrite, secondWrite);
        Assert.True(secondWriterHasLock.Task.IsCompleted);

        await using var context = database.CreateContext();
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .Where(x => x.Id == ComponentId)
            .Select(x => x.ComponentContent)
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Where(x => x.LinkedComponentId == ComponentId)
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(NewAssetUrl), content);
        Assert.Equal([NewAssetUrl], assetReferences);
    }

    [Fact]
    public async Task SaveWithContentAsync_AndSaveContentAsync_SerializeThroughSameComponentLock()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentWithoutAssetsAsync();
        var aggregateWriterHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAggregateWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contentWriterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contentWriterHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cache = new TestPlatformMemoryCache();
        var aggregateService = new PageBuilderLinkedComponentService(
            () => new CoordinatedPageBuilderModuleRepository(
                database.CreateContext(),
                async cancellationToken =>
                {
                    aggregateWriterHasLock.SetResult();
                    await releaseAggregateWriter.Task.WaitAsync(cancellationToken);
                }),
            cache,
            new RecordingEventPublisher());
        var contentService = new PageBuilderLinkedComponentContentService(
            () => new CoordinatedPageBuilderModuleRepository(
                database.CreateContext(),
                _ =>
                {
                    contentWriterHasLock.SetResult();
                    return Task.CompletedTask;
                }),
            new RecordingEventPublisher());

        var aggregateWrite = aggregateService.SaveWithContentAsync(
            new PageBuilderLinkedComponent
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Shared hero",
            },
            ContentWithAsset(OldAssetUrl),
            TestContext.Current.CancellationToken);
        await aggregateWriterHasLock.Task.WaitAsync(TestContext.Current.CancellationToken);

        var contentWrite = Task.Run(
            async () =>
            {
                contentWriterStarted.SetResult();
                await contentService.SaveContentAsync(
                    ComponentId,
                    ContentWithAsset(NewAssetUrl),
                    TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken);
        await contentWriterStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(contentWriterHasLock.Task.IsCompleted);

        releaseAggregateWriter.SetResult();
        await Task.WhenAll(aggregateWrite, contentWrite);
        Assert.True(contentWriterHasLock.Task.IsCompleted);

        await using var context = database.CreateContext();
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .Where(x => x.Id == ComponentId)
            .Select(x => x.ComponentContent)
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Where(x => x.LinkedComponentId == ComponentId)
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(NewAssetUrl), content);
        Assert.Equal([NewAssetUrl], assetReferences);
    }

    [Fact]
    public async Task RebuildIndexAsync_WhenPassedContentIsStale_UsesCurrentPersistedContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(NewAssetUrl);
        var service = new PageBuilderLinkedComponentAssetReferenceIndexService(database.RepositoryFactory);

        await service.RebuildIndexAsync(
            ComponentId,
            ContentWithAsset(OldAssetUrl),
            TestContext.Current.CancellationToken);

        await using var context = database.CreateContext();
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Where(x => x.LinkedComponentId == ComponentId)
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal([NewAssetUrl], assetReferences);
    }

    [Fact]
    public async Task UpdateMetadataAsync_UpdatesOnlyMetadataAndPreservesRequiredContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);
        var model = new PageBuilderLinkedComponent
        {
            Id = ComponentId,
            StoreId = StoreId,
            Name = " Renamed shared hero ",
        };

        var result = await service.UpdateMetadataAsync(
            model,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Renamed shared hero", result.Name);
        Assert.Equal(
            [
                typeof(PageBuilderLinkedComponentChangingEvent),
                typeof(PageBuilderLinkedComponentChangedEvent),
            ],
            events.EventTypes);

        await using var context = database.CreateContext();
        var component = await context.Set<PageBuilderLinkedComponentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Renamed shared hero", component.Name);
        Assert.Equal(ContentWithAsset(OldAssetUrl), content.ComponentContent);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenComponentWasConcurrentlyDeleted_DoesNotResurrectMetadataOnlyRow()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);
        var staleModel = new PageBuilderLinkedComponent
        {
            Id = ComponentId,
            StoreId = StoreId,
            Name = "Rename after delete",
        };

        await using (var deleteContext = database.CreateContext())
        {
            var component = await deleteContext.Set<PageBuilderLinkedComponentEntity>()
                .SingleAsync(TestContext.Current.CancellationToken);
            deleteContext.Remove(component);
            await deleteContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveChangesAsync([staleModel]));

        await using var verifyContext = database.CreateContext();
        Assert.False(await verifyContext.Set<PageBuilderLinkedComponentEntity>()
            .AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.Set<PageBuilderLinkedComponentContentEntity>()
            .AnyAsync(TestContext.Current.CancellationToken));
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task UpdateMetadataAsync_WhenIdWasRecreatedInAnotherStore_DoesNotUpdateReplacement()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var staleModel = new PageBuilderLinkedComponent();

        await using (var replaceContext = database.CreateContext())
        {
            var oldComponent = await replaceContext.Set<PageBuilderLinkedComponentEntity>()
                .SingleAsync(TestContext.Current.CancellationToken);
            oldComponent.ToModel(staleModel);
            replaceContext.Remove(oldComponent);
            await replaceContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            replaceContext.Add(new PageBuilderLinkedComponentEntity
            {
                Id = ComponentId,
                StoreId = "other-store",
                Name = "Replacement component",
                CreatedDate = DateTime.UtcNow.AddMinutes(1),
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = ComponentId,
                    ComponentContent = "{ \"settings\": {}, \"content\": [] }",
                },
            });
            await replaceContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        staleModel.Name = "Unauthorized stale rename";
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);

        var result = await service.UpdateMetadataAsync(
            staleModel,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        await using var verifyContext = database.CreateContext();
        var replacement = await verifyContext.Set<PageBuilderLinkedComponentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("other-store", replacement.StoreId);
        Assert.Equal("Replacement component", replacement.Name);
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task TrySaveContentAsync_WhenAuthorizedIdWasRecreatedInSameStore_DoesNotModifyReplacement()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var authorizedSnapshot = await database.LoadComponentAsync();
        var replacementCreatedDate = authorizedSnapshot.CreatedDate.AddMinutes(1);
        await database.ReplaceComponentAsync(StoreId, replacementCreatedDate, ReplacementAssetUrl);
        await database.SeedReferencingPagesAsync();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentContentService(database.RepositoryFactory, events);

        var saved = await service.TrySaveContentAsync(
            authorizedSnapshot,
            "{}",
            TestContext.Current.CancellationToken);

        Assert.False(saved);
        await using var context = database.CreateContext();
        var replacement = await context.Set<PageBuilderLinkedComponentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var replacementContent = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .Select(x => x.ComponentContent)
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var pageModifiedDate = await context.Set<PageBuilderPageEntity>()
            .Where(x => x.Id == ReferencingPageId)
            .Select(x => x.ModifiedDate)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StoreId, replacement.StoreId);
        Assert.Equal(replacementCreatedDate, replacement.CreatedDate);
        Assert.Equal("Replacement component", replacement.Name);
        Assert.Equal(ContentWithAsset(ReplacementAssetUrl), replacementContent);
        Assert.Equal([ReplacementAssetUrl], assetReferences);
        Assert.Equal(OldPageModifiedDate, pageModifiedDate);
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task TryLoadContentAsync_WhenAuthorizedIdWasRecreatedInAnotherStore_DoesNotReturnReplacementContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var authorizedSnapshot = await database.LoadComponentAsync();
        await database.ReplaceComponentAsync(
            "other-store",
            authorizedSnapshot.CreatedDate.AddMinutes(1),
            ReplacementAssetUrl);
        var service = new PageBuilderLinkedComponentContentService(
            database.RepositoryFactory,
            new RecordingEventPublisher());

        var content = await service.TryLoadContentAsync(
            authorizedSnapshot,
            TestContext.Current.CancellationToken);

        Assert.Null(content);
    }

    [Fact]
    public async Task TryDeleteAsync_WhenAuthorizedIdWasRecreatedInAnotherStore_DoesNotDeleteReplacement()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var authorizedSnapshot = await database.LoadComponentAsync();
        var replacementCreatedDate = authorizedSnapshot.CreatedDate.AddMinutes(1);
        await database.ReplaceComponentAsync("other-store", replacementCreatedDate, ReplacementAssetUrl);
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);

        var deleted = await service.TryDeleteAsync(
            authorizedSnapshot,
            TestContext.Current.CancellationToken);

        Assert.False(deleted);
        await using var context = database.CreateContext();
        var replacement = await context.Set<PageBuilderLinkedComponentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var replacementContent = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .Select(x => x.ComponentContent)
            .SingleAsync(TestContext.Current.CancellationToken);
        var assetReferences = await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Select(x => x.NormalizedAssetUrl)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal("other-store", replacement.StoreId);
        Assert.Equal(replacementCreatedDate, replacement.CreatedDate);
        Assert.Equal("Replacement component", replacement.Name);
        Assert.Equal(ContentWithAsset(ReplacementAssetUrl), replacementContent);
        Assert.Equal([ReplacementAssetUrl], assetReferences);
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task TryDeleteAsync_WhenIdentityMatches_DeletesCompleteAggregateAndPublishesEvents()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var authorizedSnapshot = await database.LoadComponentAsync();
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);

        var deleted = await service.TryDeleteAsync(
            authorizedSnapshot,
            TestContext.Current.CancellationToken);

        Assert.True(deleted);
        await using var context = database.CreateContext();
        Assert.False(await context.Set<PageBuilderLinkedComponentEntity>()
            .AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await context.Set<PageBuilderLinkedComponentContentEntity>()
            .AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await context.Set<PageBuilderLinkedComponentAssetReferenceEntity>()
            .AnyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            [
                typeof(PageBuilderLinkedComponentChangingEvent),
                typeof(PageBuilderLinkedComponentChangedEvent),
            ],
            events.EventTypes);
    }

    [Fact]
    public async Task ConditionalOperations_WhenRequiredContentIsMissing_RefuseWithoutRepairingOrDeletingMetadata()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedMetadataOnlyComponentAsync();
        var authorizedSnapshot = await database.LoadComponentAsync();
        var contentEvents = new RecordingEventPublisher();
        var contentService = new PageBuilderLinkedComponentContentService(database.RepositoryFactory, contentEvents);
        using var cache = new TestPlatformMemoryCache();
        var componentEvents = new RecordingEventPublisher();
        var componentService = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, componentEvents);

        var loadedContent = await contentService.TryLoadContentAsync(
            authorizedSnapshot,
            TestContext.Current.CancellationToken);
        var saved = await contentService.TrySaveContentAsync(
            authorizedSnapshot,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);
        var deleted = await componentService.TryDeleteAsync(
            authorizedSnapshot,
            TestContext.Current.CancellationToken);

        Assert.Null(loadedContent);
        Assert.False(saved);
        Assert.False(deleted);
        await using var context = database.CreateContext();
        Assert.True(await context.Set<PageBuilderLinkedComponentEntity>()
            .AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await context.Set<PageBuilderLinkedComponentContentEntity>()
            .AnyAsync(TestContext.Current.CancellationToken));
        Assert.Empty(contentEvents.EventTypes);
        Assert.Empty(componentEvents.EventTypes);
    }

    [Fact]
    public async Task TryLoadContentAsync_DoesNotAcquireAWriteLock()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var expectedComponent = await database.LoadComponentAsync();
        var service = new PageBuilderLinkedComponentContentService(
            () => new ReadOnlyGuardRepository(database.CreateContext()),
            new RecordingEventPublisher());

        var content = await service.TryLoadContentAsync(
            expectedComponent,
            TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(OldAssetUrl), content);
    }

    [Fact]
    public async Task SaveWithContentAsync_WhenIdWasRecreatedInSameStore_DoesNotOverwriteReplacement()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var staleModel = await database.LoadComponentAsync();
        await database.ReplaceComponentAsync(
            StoreId,
            staleModel.CreatedDate.AddMinutes(1),
            ReplacementAssetUrl);
        staleModel.Name = "Stale aggregate";
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveWithContentAsync(
            staleModel,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken));

        await using var context = database.CreateContext();
        var replacement = await context.Set<PageBuilderLinkedComponentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var replacementContent = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .Select(x => x.ComponentContent)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Replacement component", replacement.Name);
        Assert.Equal(ContentWithAsset(ReplacementAssetUrl), replacementContent);
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task SaveWithContentAsync_WhenComponentWasDeleted_DoesNotRecreateIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        var staleModel = await database.LoadComponentAsync();
        await using (var context = database.CreateContext())
        {
            var component = await context.Set<PageBuilderLinkedComponentEntity>()
                .SingleAsync(TestContext.Current.CancellationToken);
            context.Remove(component);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveWithContentAsync(
            staleModel,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken));

        await using var verificationContext = database.CreateContext();
        Assert.False(await verificationContext.Set<PageBuilderLinkedComponentEntity>()
            .AnyAsync(TestContext.Current.CancellationToken));
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task SaveWithContentAsync_NewExplicitImportId_PersistsAggregate()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var cache = new TestPlatformMemoryCache();
        var service = new PageBuilderLinkedComponentService(
            database.RepositoryFactory,
            cache,
            new RecordingEventPublisher());
        var model = new PageBuilderLinkedComponent
        {
            Id = "imported-component",
            StoreId = StoreId,
            Name = "Imported component",
        };

        await service.SaveWithContentAsync(
            model,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);

        await using var context = database.CreateContext();
        var component = await context.Set<PageBuilderLinkedComponentEntity>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(model.Id, component.Id);
        Assert.True(await context.Set<PageBuilderLinkedComponentContentEntity>()
            .AnyAsync(x => x.Id == model.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenSecondComponentWasRecreated_RollsBackWholeBatch()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync("component-a", "Component A", OldAssetUrl, FirstCreatedDate);
        await database.SeedComponentAsync("component-b", "Component B", OldAssetUrl, SecondCreatedDate);
        var first = await database.LoadComponentAsync("component-a");
        var second = await database.LoadComponentAsync("component-b");
        await database.ReplaceComponentAsync(
            "component-b",
            StoreId,
            SecondCreatedDate.AddMinutes(1),
            ReplacementAssetUrl);
        first.Name = "Updated A";
        second.Name = "Updated B";
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveChangesAsync([first, second]));

        await using var context = database.CreateContext();
        var names = await context.Set<PageBuilderLinkedComponentEntity>()
            .ToDictionaryAsync(x => x.Id, x => x.Name, TestContext.Current.CancellationToken);
        Assert.Equal("Component A", names["component-a"]);
        Assert.Equal("Replacement component", names["component-b"]);
        Assert.Empty(events.EventTypes);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenDatabaseRejectsSecondUpdate_RollsBackWholeBatch()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync("component-a", "Component A", OldAssetUrl, FirstCreatedDate);
        await database.SeedComponentAsync("component-b", "Component B", OldAssetUrl, SecondCreatedDate);
        var first = await database.LoadComponentAsync("component-a");
        var second = await database.LoadComponentAsync("component-b");
        first.Name = "Updated A";
        second.Name = "Updated B";
        await database.FailSecondComponentUpdatesAsync();
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.SaveChangesAsync([first, second]));

        await using var context = database.CreateContext();
        var names = await context.Set<PageBuilderLinkedComponentEntity>()
            .ToDictionaryAsync(x => x.Id, x => x.Name, TestContext.Current.CancellationToken);
        Assert.Equal("Component A", names["component-a"]);
        Assert.Equal("Component B", names["component-b"]);
        Assert.Equal([typeof(PageBuilderLinkedComponentChangingEvent)], events.EventTypes);
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesBatchAndPreservesAuditOwnership()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync("component-a", "Component A", OldAssetUrl, FirstCreatedDate);
        await database.SeedComponentAsync("component-b", "Component B", OldAssetUrl, SecondCreatedDate);
        var first = await database.LoadComponentAsync("component-a");
        var second = await database.LoadComponentAsync("component-b");
        first.Name = "Updated A";
        second.Name = "Updated B";
        first.CreatedBy = "spoofed-creator";
        first.ModifiedBy = "spoofed-modifier";
        using var cache = new TestPlatformMemoryCache();
        var events = new RecordingEventPublisher();
        var service = new PageBuilderLinkedComponentService(database.RepositoryFactory, cache, events);

        await service.SaveChangesAsync([first, second]);

        await using var context = database.CreateContext();
        var components = await context.Set<PageBuilderLinkedComponentEntity>()
            .OrderBy(x => x.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["Updated A", "Updated B"], components.Select(x => x.Name));
        Assert.All(components, component => Assert.Equal(OriginalCreator, component.CreatedBy));
        Assert.All(components, component => Assert.Equal(OriginalModifier, component.ModifiedBy));
        Assert.All(components, component => Assert.True(component.ModifiedDate > OldComponentModifiedDate));
        Assert.Equal(
            [typeof(PageBuilderLinkedComponentChangingEvent), typeof(PageBuilderLinkedComponentChangedEvent)],
            events.EventTypes);
    }

    [Fact]
    public async Task SaveContentAsync_WithManyUsages_DoesNotUpdatePageRowsOrFanOutCommands()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedComponentAsync(OldAssetUrl);
        await database.SeedManyReferencingPagesAsync(1000);
        var commands = new CountingCommandInterceptor();
        var service = new PageBuilderLinkedComponentContentService(
            () => new PageBuilderModuleRepository(database.CreateContext(commands)),
            new RecordingEventPublisher());

        await service.SaveContentAsync(
            ComponentId,
            ContentWithAsset(NewAssetUrl),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, commands.PageUpdateCount);
        Assert.True(commands.CommandCount < 20, $"Expected a bounded command count, got {commands.CommandCount}.");
    }

    private static string ContentWithAsset(string assetUrl)
    {
        return $"{{ \"settings\": {{ \"image\": \"{assetUrl}\" }}, \"content\": [] }}";
    }

    private static async Task AssertContentAndRecoveryMarkerWereCommittedAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        var content = await context.Set<PageBuilderLinkedComponentContentEntity>()
            .Where(x => x.Id == ComponentId)
            .Select(x => x.ComponentContent)
            .SingleAsync(TestContext.Current.CancellationToken);
        var pageModifiedDates = await context.Set<PageBuilderPageEntity>()
            .ToDictionaryAsync(x => x.Id, x => x.ModifiedDate, TestContext.Current.CancellationToken);
        var componentModifiedDate = await context.Set<PageBuilderLinkedComponentEntity>()
            .Where(x => x.Id == ComponentId)
            .Select(x => x.ModifiedDate)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ContentWithAsset(NewAssetUrl), content);
        Assert.NotNull(componentModifiedDate);
        Assert.Equal(OldPageModifiedDate, pageModifiedDates[ReferencingPageId]);
        Assert.Equal(OldPageModifiedDate, pageModifiedDates[UnrelatedPageId]);
    }

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public IList<Type> EventTypes { get; } = [];

        public Task Publish<T>(T @event, CancellationToken cancellationToken = default)
            where T : IEvent
        {
            EventTypes.Add(@event.GetType());
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingContentChangedEventPublisher : IEventPublisher
    {
        public Task Publish<T>(T @event, CancellationToken cancellationToken = default)
            where T : IEvent
        {
            if (@event is PageBuilderLinkedComponentContentChangedEvent)
            {
                throw new InvalidOperationException("Simulated post-commit propagation failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestPlatformMemoryCache : IPlatformMemoryCache
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        public ICacheEntry CreateEntry(object key) => _cache.CreateEntry(key);

        public void Remove(object key) => _cache.Remove(key);

        public bool TryGetValue(object key, out object value) => _cache.TryGetValue(key, out value);

        public MemoryCacheEntryOptions GetDefaultCacheEntryOptions() => new();

        public void Dispose() => _cache.Dispose();
    }

    private sealed class CoordinatedPageBuilderModuleRepository(
        PageBuilderModuleDbContext dbContext,
        Func<CancellationToken, Task> afterLock)
        : PageBuilderModuleRepository(dbContext)
    {
        public override Task<bool> ExecuteUnderLinkedComponentWriteLockAsync(
            string linkedComponentId,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            return base.ExecuteUnderLinkedComponentWriteLockAsync(
                linkedComponentId,
                async transactionCancellationToken =>
                {
                    await afterLock(transactionCancellationToken);
                    await operation(transactionCancellationToken);
                },
                cancellationToken);
        }
    }

    private sealed class ReadOnlyGuardRepository(PageBuilderModuleDbContext dbContext)
        : PageBuilderModuleRepository(dbContext)
    {
        public override Task<bool> ExecuteUnderLinkedComponentWriteLockAsync(
            string linkedComponentId,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("A read path attempted to acquire a write lock.");
        }
    }

    private sealed class CountingCommandInterceptor : DbCommandInterceptor
    {
        public int CommandCount { get; private set; }
        public int PageUpdateCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Record(DbCommand command)
        {
            CommandCount++;
            if (command.CommandText.Contains("UPDATE \"PageBuilderPage\"", StringComparison.OrdinalIgnoreCase))
            {
                PageUpdateCount++;
            }
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _anchorConnection;

        private TestDatabase(string connectionString, SqliteConnection anchorConnection)
        {
            ConnectionString = connectionString;
            _anchorConnection = anchorConnection;
        }

        public string ConnectionString { get; }

        public Func<IPageBuilderModuleRepository> RepositoryFactory =>
            () => new PageBuilderModuleRepository(CreateContext());

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"page-builder-component-aggregate-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 10,
            }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new TestDatabase(connectionString, anchorConnection);

            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return database;
        }

        public PageBuilderModuleDbContext CreateContext(DbCommandInterceptor interceptor = null)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
                .UseSqlite(ConnectionString);
            if (interceptor != null)
            {
                optionsBuilder.AddInterceptors(interceptor);
            }

            var options = optionsBuilder.Options;
            return new PageBuilderModuleDbContext(options);
        }

        public async Task SeedComponentAsync(string assetUrl)
        {
            await SeedComponentAsync(ComponentId, "Shared hero", assetUrl, DateTime.UtcNow);
        }

        public async Task SeedComponentAsync(
            string id,
            string name,
            string assetUrl,
            DateTime createdDate)
        {
            await using var context = CreateContext();
            context.Add(new PageBuilderLinkedComponentEntity
            {
                Id = id,
                StoreId = StoreId,
                Name = name,
                CreatedBy = OriginalCreator,
                ModifiedBy = OriginalModifier,
                CreatedDate = createdDate,
                ModifiedDate = OldComponentModifiedDate,
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = id,
                    ComponentContent = ContentWithAsset(assetUrl),
                },
            });
            context.Add(new PageBuilderLinkedComponentAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                LinkedComponentId = id,
                NormalizedAssetUrl = assetUrl,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(assetUrl),
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task SeedComponentWithoutAssetsAsync()
        {
            await using var context = CreateContext();
            context.Add(new PageBuilderLinkedComponentEntity
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Shared hero",
                CreatedDate = DateTime.UtcNow,
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = ComponentId,
                    ComponentContent = "{ \"settings\": {}, \"content\": [] }",
                },
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task SeedMetadataOnlyComponentAsync()
        {
            await using var context = CreateContext();
            context.Add(new PageBuilderLinkedComponentEntity
            {
                Id = ComponentId,
                StoreId = StoreId,
                Name = "Incomplete component",
                CreatedDate = DateTime.UtcNow,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task<PageBuilderLinkedComponent> LoadComponentAsync()
        {
            return await LoadComponentAsync(ComponentId);
        }

        public async Task<PageBuilderLinkedComponent> LoadComponentAsync(string id)
        {
            await using var context = CreateContext();
            var entity = await context.Set<PageBuilderLinkedComponentEntity>()
                .SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
            return entity.ToModel(new PageBuilderLinkedComponent());
        }

        public async Task ReplaceComponentAsync(string storeId, DateTime createdDate, string assetUrl)
        {
            await ReplaceComponentAsync(ComponentId, storeId, createdDate, assetUrl);
        }

        public async Task ReplaceComponentAsync(
            string id,
            string storeId,
            DateTime createdDate,
            string assetUrl)
        {
            await using (var deleteContext = CreateContext())
            {
                var oldComponent = await deleteContext.Set<PageBuilderLinkedComponentEntity>()
                    .SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
                deleteContext.Remove(oldComponent);
                await deleteContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var createContext = CreateContext();
            createContext.Add(new PageBuilderLinkedComponentEntity
            {
                Id = id,
                StoreId = storeId,
                Name = "Replacement component",
                CreatedDate = createdDate,
                Content = new PageBuilderLinkedComponentContentEntity
                {
                    Id = id,
                    ComponentContent = ContentWithAsset(assetUrl),
                },
            });
            createContext.Add(new PageBuilderLinkedComponentAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                LinkedComponentId = id,
                NormalizedAssetUrl = assetUrl,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(assetUrl),
            });
            await createContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task SeedReferencingPagesAsync()
        {
            await using var context = CreateContext();
            context.Add(new GroupedPageBuilderPageEntity
            {
                Id = GroupId,
                StoreId = StoreId,
                CultureName = "en-US",
                Name = "Page group",
                CreatedDate = OldPageModifiedDate,
            });
            context.AddRange(
                new PageBuilderPageEntity
                {
                    Id = ReferencingPageId,
                    GroupId = GroupId,
                    StoreId = StoreId,
                    Status = "Published",
                    CreatedDate = OldPageModifiedDate,
                    ModifiedDate = OldPageModifiedDate,
                },
                new PageBuilderPageEntity
                {
                    Id = UnrelatedPageId,
                    GroupId = GroupId,
                    StoreId = StoreId,
                    Status = "Published",
                    CreatedDate = OldPageModifiedDate,
                    ModifiedDate = OldPageModifiedDate,
                });
            context.Add(new PageBuilderLinkedComponentReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                PageId = ReferencingPageId,
                LinkedComponentId = ComponentId,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task SeedManyReferencingPagesAsync(int count)
        {
            await using var context = CreateContext();
            const string groupId = "fanout-group";
            context.Add(new GroupedPageBuilderPageEntity
            {
                Id = groupId,
                StoreId = StoreId,
                CultureName = "en-US",
                Name = "Fan-out pages",
                CreatedDate = OldPageModifiedDate,
            });

            for (var index = 0; index < count; index++)
            {
                var pageId = $"fanout-page-{index:D4}";
                context.Add(new PageBuilderPageEntity
                {
                    Id = pageId,
                    GroupId = groupId,
                    StoreId = StoreId,
                    Status = "Published",
                    CreatedDate = OldPageModifiedDate,
                    ModifiedDate = OldPageModifiedDate,
                });
                context.Add(new PageBuilderLinkedComponentReferenceEntity
                {
                    Id = $"fanout-reference-{index:D4}",
                    PageId = pageId,
                    LinkedComponentId = ComponentId,
                });
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task FailSecondComponentUpdatesAsync()
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER FailSecondComponentUpdate
                BEFORE UPDATE ON PageBuilderLinkedComponent
                WHEN OLD.Id = 'component-b' AND NEW.Name = 'Updated B'
                BEGIN
                    SELECT RAISE(ABORT, 'simulated second component update failure');
                END;
                """,
                TestContext.Current.CancellationToken);
        }

        public async Task FailComponentAssetReferenceInsertsAsync()
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER FailComponentAssetReferenceInsert
                BEFORE INSERT ON PageBuilderLinkedComponentAssetReference
                BEGIN
                    SELECT RAISE(ABORT, 'simulated component asset index failure');
                END;
                """,
                TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }
    }

    private const string StoreId = "store";
    private const string ComponentId = "component";
    private const string GroupId = "group";
    private const string ReferencingPageId = "page-referencing-component";
    private const string UnrelatedPageId = "page-without-component";
    private const string OldAssetUrl = "/stores/store/old.png";
    private const string NewAssetUrl = "/stores/store/new.png";
    private const string ReplacementAssetUrl = "/stores/store/replacement.png";
    private static readonly DateTime OldPageModifiedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OldComponentModifiedDate = new(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FirstCreatedDate = new(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SecondCreatedDate = new(2025, 2, 2, 0, 0, 0, DateTimeKind.Utc);
    private const string OriginalCreator = "creator";
    private const string OriginalModifier = "previous-modifier";
}
