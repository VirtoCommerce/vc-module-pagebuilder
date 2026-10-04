using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public partial class PageContentAtomicWriteTests
{
    [Fact]
    public async Task ContentGet_RealServiceTokenSupportsTwoSuccessiveSaves()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        using var cache = new TestPlatformMemoryCache();
        var service = CreateGroupedService(database.ConnectionString, cache);
        var controller = CreateContentController(database.ConnectionString, cache, service);

        await controller.GetPageContent(GroupId, true, TestContext.Current.CancellationToken);
        Assert.Equal(ComponentAContent, Encoding.UTF8.GetString(((MemoryStream)controller.Response.Body).ToArray()));
        var eTag = controller.Response.Headers.ETag.ToString();
        foreach (var content in new[] { ComponentBContent, UpdatedPageContent })
        {
            controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(content));
            controller.Request.Headers.IfMatch = eTag;
            Assert.IsType<NoContentResult>(await controller.SavePageContent(GroupId, TestContext.Current.CancellationToken));
            eTag = controller.Response.Headers.ETag.ToString();
        }
        Assert.Equal(UpdatedPageContent, await LoadContentAsync(database.ConnectionString));
        Assert.Equal(PageBuilderContentVersion.Create(await LoadGroupAsync(database.ConnectionString), UpdatedPageContent), eTag);
    }

    [Fact]
    public async Task ContentGet_IgnoresCachedMembershipAndUsesSameDraftAsSaveAndPublish()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        using var cache = new TestPlatformMemoryCache();
        var service = CreateGroupedService(database.ConnectionString, cache);
        var cached = await service.GetByIdAsync(GroupId);
        await using (var context = CreateContext(database.ConnectionString))
        {
            context.Add(new PageBuilderPageEntity
            {
                Id = "newer-draft",
                GroupId = GroupId,
                StoreId = StoreId,
                Status = "Draft",
                ModifiedDate = DateTime.UtcNow.AddDays(1),
                Content = new PageBuilderContentEntity { PageContent = ComponentBContent },
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        Assert.DoesNotContain(cached.Pages, x => x.Id == "newer-draft");
        var snapshot = await service.LoadGroupContentAsync(cached, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("newer-draft", snapshot.PageId);
        var written = await service.SaveGroupContentAsync(cached, UpdatedPageContent, snapshot.ETag, TestContext.Current.CancellationToken);
        Assert.Equal(snapshot.PageId, written.PageId);
        var controller = CreateContentController(database.ConnectionString, cache, service);
        Assert.IsType<OkResult>(await controller.PublishGroup(GroupId, true, TestContext.Current.CancellationToken));
        var published = await service.LoadGroupContentAsync(cached, false, TestContext.Current.CancellationToken);
        Assert.Equal(written.PageId, published.PageId);
        Assert.Equal(UpdatedPageContent, published.Content);
    }

    [Fact]
    public async Task ConditionalSave_IdenticalRetryReturnsCurrentVersionWithoutAnotherWrite()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        var group = await LoadGroupAsync(database.ConnectionString);
        var version = PageBuilderContentVersion.Create(group, ComponentAContent);
        var saved = await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent, version);
        var retried = await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent, version,
            _ => throw new InvalidOperationException("An identical retry must not write or rebuild indexes."));
        Assert.Equal(saved, retried);
    }

    [Fact]
    public async Task ConditionalSave_CaseOnlyAuthorizedStoreDifferenceIsAccepted()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SaveAsync(database.ConnectionString, ComponentAContent);
        var group = await LoadGroupAsync(database.ConnectionString);
        var eTag = PageBuilderContentVersion.Create(group, ComponentAContent);
        group.StoreId = group.StoreId.ToUpperInvariant();
        var result = await SaveConditionalAsync(database.ConnectionString, group, ComponentBContent, eTag);
        Assert.Equal(PageBuilderContentVersion.Create(await LoadGroupAsync(database.ConnectionString), ComponentBContent), result.ETag);
    }

    [Fact]
    public async Task ConditionalSave_FirstDraftRaisesGroupedEventsAndUpdatesGroupAuditDate()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = CreateContext(database.ConnectionString))
        {
            context.Remove(await context.Set<PageBuilderPageEntity>().SingleAsync(x => x.Id == PageId, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var group = await LoadGroupAsync(database.ConnectionString);
        using var cache = new TestPlatformMemoryCache();
        var events = new ReviewEventPublisher();
        var service = CreateGroupedService(database.ConnectionString, cache, events);
        var result = await service.SaveGroupContentAsync(group, ComponentAContent, PageBuilderContentVersion.Create(group, null), TestContext.Current.CancellationToken);
        var changing = Assert.Single(events.Events.OfType<GroupedPageBuilderPageChangingEvent>());
        var changed = Assert.Single(events.Events.OfType<GroupedPageBuilderPageChangedEvent>());
        Assert.Contains(Assert.Single(changing.ChangedEntries).NewEntry.Pages, x => x.Id == result.PageId);
        Assert.Contains(Assert.Single(changed.ChangedEntries).NewEntry.Pages, x => x.Id == result.PageId);
        Assert.NotNull((await service.GetByIdAsync(GroupId)).ModifiedDate);

        events.Events.Clear();
        var retry = await service.SaveGroupContentAsync(group, ComponentAContent, PageBuilderContentVersion.Create(group, null), TestContext.Current.CancellationToken);
        Assert.Equal(result, retry);
        Assert.Empty(events.Events);
    }

    [Fact]
    public async Task GroupedCreate_PersistsSharedComponentAndAssetIndexesWithContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var cache = new TestPlatformMemoryCache();
        var service = CreateGroupedService(database.ConnectionString, cache);
        var group = new GroupedPageBuilderPage
        {
            StoreId = StoreId,
            Name = "Imported",
            Pages = [new PageBuilderPage { StoreId = StoreId, Status = "Draft", Content = ComponentAContent }],
        };
        await service.SaveChangesAsync([group]);
        var pageId = Assert.Single(group.Pages).Id;
        await using var context = CreateContext(database.ConnectionString);
        Assert.Equal([ComponentAId], await context.Set<PageBuilderSharedComponentReferenceEntity>().Where(x => x.PageId == pageId)
            .Select(x => x.SharedComponentId).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal([AssetAUrl], await context.Set<PageBuilderAssetReferenceEntity>().Where(x => x.PageId == pageId)
            .Select(x => x.NormalizedAssetUrl).ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GroupedCreate_InvalidReferenceRollsBackMetadataContentAndIndexes()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var cache = new TestPlatformMemoryCache();
        var service = CreateGroupedService(database.ConnectionString, cache);
        var group = new GroupedPageBuilderPage
        {
            Id = "invalid-import",
            StoreId = StoreId,
            Pages = [new PageBuilderPage { StoreId = StoreId, Status = "Draft", Content = ComponentAContent.Replace(ComponentAId, "missing") }],
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => service.SaveChangesAsync([group]));
        await using var context = CreateContext(database.ConnectionString);
        Assert.False(await context.Set<GroupedPageBuilderPageEntity>().AnyAsync(x => x.Id == group.Id, TestContext.Current.CancellationToken));
        Assert.False(await context.Set<PageBuilderPageEntity>().AnyAsync(x => x.GroupId == group.Id, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupSettings_VisibilityOnlyPreservesDocumentBytesAndVersion(bool differentContentSettings)
    {
        await using var database = await TestDatabase.CreateAsync();
        var content = differentContentSettings
            ? ComponentAContent.Replace("\"settings\":{}", "\"settings\":{\"name\":\"Content-owned name\"}")
            : ComponentAContent;
        await SaveAsync(database.ConnectionString, content);
        using var cache = new TestPlatformMemoryCache();
        var service = CreateGroupedService(database.ConnectionString, cache);
        var group = await service.GetByIdAsync(GroupId);
        var before = await service.LoadGroupContentAsync(group, cancellationToken: TestContext.Current.CancellationToken);
        group.Visibility = !group.Visibility;
        await service.SaveGroupSettingsAsync(group, TestContext.Current.CancellationToken);
        var after = await service.LoadGroupContentAsync(group, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(before, after);
        await service.SaveGroupContentAsync(group, ComponentBContent, before.ETag, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupSettings_CachedShellModelPreservesLatestDesignerContent(bool firstDraft)
    {
        await using var database = await TestDatabase.CreateAsync();
        if (firstDraft)
        {
            await using var context = CreateContext(database.ConnectionString);
            context.Remove(await context.Set<PageBuilderPageEntity>().SingleAsync(x => x.Id == PageId, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await SaveRawAsync(database.ConnectionString, SourcePageId, ComponentAContent);
        }
        else
            await SaveAsync(database.ConnectionString, ComponentAContent);
        using var cache = new TestPlatformMemoryCache();
        var service = CreateGroupedService(database.ConnectionString, cache);
        var shellModel = await service.GetByIdAsync(GroupId);
        var snapshot = await service.LoadGroupContentAsync(shellModel, cancellationToken: TestContext.Current.CancellationToken);
        var saved = await service.SaveGroupContentAsync(shellModel, ComponentBContent, snapshot.ETag, TestContext.Current.CancellationToken);
        shellModel.Name = "Renamed";
        await service.SaveGroupSettingsAsync(shellModel, TestContext.Current.CancellationToken);
        var after = await service.LoadGroupContentAsync(shellModel, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(saved.PageId, after.PageId);
        Assert.Contains(ComponentBId, after.Content);
        Assert.Contains("Renamed", after.Content);
        Assert.Single(shellModel.Pages, x => x.Status == "Draft");
        await using var verify = CreateContext(database.ConnectionString);
        Assert.Equal([ComponentBId], await verify.Set<PageBuilderSharedComponentReferenceEntity>().Where(x => x.PageId == saved.PageId)
            .Select(x => x.SharedComponentId).ToArrayAsync(TestContext.Current.CancellationToken));
    }

    private static GroupedPageService CreateGroupedService(string connectionString, TestPlatformMemoryCache cache, IEventPublisher events = null)
        => new(() => new PageBuilderModuleRepository(CreateContext(connectionString)),
            () => new SqliteContentStreamRepository(CreateContext(connectionString)), cache, events ?? new NoopEventPublisher(),
            NullLogger<GroupedPageService>.Instance);

    private static PageBuilderPageController CreateContentController(string connectionString, TestPlatformMemoryCache cache, GroupedPageService service)
    {
        var events = new NoopEventPublisher();
        var pages = new PageBuilderPageService(() => new PageBuilderModuleRepository(CreateContext(connectionString)), cache, events);
        var content = new PageBuilderPageContentService(pages, service, new NoopSharedComponentReferenceIndexService(), events,
            NullLogger<PageBuilderPageContentService>.Instance);
        return new PageBuilderPageController(pages, service, null, new PublishedRenameContentPreservationTests.AllowAllAuthorizationService(),
            null, content, NullLogger<PageBuilderPageController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } } },
        };
    }

    private sealed class ReviewEventPublisher : IEventPublisher
    {
        public List<IEvent> Events { get; } = [];
        public Task Publish<T>(T @event, CancellationToken cancellationToken = default) where T : IEvent
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }
}
