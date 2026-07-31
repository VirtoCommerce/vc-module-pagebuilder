using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Common;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderPageControllerSharedComponentPreflightTests
{
    [Fact]
    public async Task SavePageContent_InvalidReferenceDoesNotCreateDraft()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        var preflight = new ThrowingReferenceIndexService();
        var controller = CreateController(service, preflight, ComponentReferenceContent);

        var result = await controller.SavePageContent("group", TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain((await service.GetByIdAsync("group")).Pages, x => x.Status == Draft);
        Assert.Equal(1, preflight.CallCount);
    }

    [Fact]
    public async Task SavePageContent_ReferenceDeletedAfterPreflightRemovesNewDraft()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        service.SaveContentException = new InvalidDataException("Linked Component 'deleted' was not found.");
        var controller = CreateController(
            service,
            new NoopLinkedComponentReferenceIndexService(),
            ComponentReferenceContent);

        var result = await controller.SavePageContent("group", TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain((await service.GetByIdAsync("group")).Pages, x => x.Status == Draft);
    }

    [Fact]
    public async Task SavePageContent_FailedWriterDoesNotDeleteDraftFilledByConcurrentWriter()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        service.ConcurrentContentBeforeSaveFailure = "{ \"concurrent\": true }";
        service.SaveContentException = new InvalidDataException("Linked Component 'deleted' was not found.");
        var controller = CreateController(
            service,
            new NoopLinkedComponentReferenceIndexService(),
            ComponentReferenceContent);

        var result = await controller.SavePageContent("group", TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        var draft = Assert.Single((await service.GetByIdAsync("group")).Pages, x => x.Status == Draft);
        Assert.Equal(
            "{ \"concurrent\": true }",
            await service.LoadContent(draft.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, service.EmptyDraftCleanupAttempts);
    }

    [Fact]
    public async Task SavePageContent_DoesNotCleanupDifferentDraftReturnedAfterSave()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        service.ReplaceNewDraftAfterSaveWithPageId = "concurrent-draft";
        service.SaveContentException = new InvalidDataException("Linked Component 'deleted' was not found.");
        var controller = CreateController(
            service,
            new NoopLinkedComponentReferenceIndexService(),
            ComponentReferenceContent);

        var result = await controller.SavePageContent("group", TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        var draft = Assert.Single((await service.GetByIdAsync("group")).Pages, x => x.Status == Draft);
        Assert.Equal("concurrent-draft", draft.Id);
        Assert.Equal(0, service.EmptyDraftCleanupAttempts);
    }

    [Fact]
    public async Task SavePageContent_MissingReadPermissionReturnsForbiddenBeforeReferencePreflight()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        var preflight = new ThrowingReferenceIndexService();
        var controller = CreateController(
            service,
            preflight,
            ComponentReferenceContent,
            new DenyPolicyAuthorizationService());

        var result = await controller.SavePageContent("group", TestContext.Current.CancellationToken);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(0, preflight.CallCount);
        Assert.DoesNotContain((await service.GetByIdAsync("group")).Pages, x => x.Status == Draft);
    }

    [Fact]
    public async Task CopyPageContent_InvalidReferenceDoesNotCreateTargetDraft()
    {
        var service = CreateServiceWithPublishedGroup("source", "source-published");
        service.SeedContent("source-published", ComponentReferenceContent);
        service.SeedGroup(new GroupedPageBuilderPage
        {
            Id = "target",
            StoreId = StoreId,
            Pages = [new PageBuilderPage { Id = "target-published", Status = Published }],
        });
        var preflight = new ThrowingReferenceIndexService();
        var controller = CreateController(service, preflight);

        var result = await controller.CopyPageContent(
            "target",
            "source",
            TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain((await service.GetByIdAsync("target")).Pages, x => x.Status == Draft);
        Assert.Equal(1, preflight.CallCount);
    }

    [Fact]
    public async Task UpdateGroup_InvalidPublishedReferenceDoesNotCreateDraftOrSaveMetadata()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        service.SeedContent("published", ComponentReferenceContent);
        var preflight = new ThrowingReferenceIndexService();
        var controller = CreateController(service, preflight);

        var result = await controller.UpdateGroup(new GroupedPageBuilderPage
        {
            Id = "group",
            StoreId = StoreId,
            Name = "Rejected rename",
        }, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        var stored = await service.GetByIdAsync("group");
        Assert.DoesNotContain(stored.Pages, x => x.Status == Draft);
        Assert.NotEqual("Rejected rename", stored.Name);
        Assert.Equal(1, preflight.CallCount);
    }

    [Fact]
    public async Task UpdateGroup_LegacyGroupWithoutReferencesCanMoveStores()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        var preflight = new NoopLinkedComponentReferenceIndexService();
        var controller = CreateController(service, preflight);

        var result = await controller.UpdateGroup(new GroupedPageBuilderPage
        {
            Id = "group",
            StoreId = "other-store",
            Name = "Moved page",
        }, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        var stored = await service.GetByIdAsync("group");
        Assert.Equal("other-store", stored.StoreId);
        Assert.Equal("Moved page", stored.Name);
        Assert.All(stored.Pages, page => Assert.Equal("other-store", page.StoreId));
    }

    [Fact]
    public async Task UpdateGroup_StoreMoveRequiresDestinationAuthorization()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        var controller = CreateController(
            service,
            new NoopLinkedComponentReferenceIndexService(),
            authorizationService: new StoreScopedAuthorizationService(StoreId));

        var result = await controller.UpdateGroup(new GroupedPageBuilderPage
        {
            Id = "group",
            StoreId = "other-store",
            Name = "Unauthorized move",
        }, TestContext.Current.CancellationToken);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(StoreId, (await service.GetByIdAsync("group")).StoreId);
    }

    [Fact]
    public async Task UpdateGroup_SpoofedTargetStoreCannotRevealExistingGroupStore()
    {
        var service = CreateServiceWithPublishedGroup("group", "published");
        var preflight = new ThrowingReferenceIndexService();
        var controller = CreateController(
            service,
            preflight,
            authorizationService: new StoreScopedAuthorizationService("authorized-store"));

        var result = await controller.UpdateGroup(new GroupedPageBuilderPage
        {
            Id = "group",
            StoreId = "authorized-store",
            Name = "Spoofed request",
        }, TestContext.Current.CancellationToken);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(StoreId, (await service.GetByIdAsync("group")).StoreId);
        Assert.Equal(0, preflight.CallCount);
    }

    private static PublishedRenameContentPreservationTests.FakeGroupedPageService CreateServiceWithPublishedGroup(
        string groupId,
        string pageId)
    {
        var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        service.SeedGroup(new GroupedPageBuilderPage
        {
            Id = groupId,
            StoreId = StoreId,
            Name = "Original",
            Pages = [new PageBuilderPage { Id = pageId, Status = Published }],
        });
        service.SeedContent(pageId, "{ \"settings\": {}, \"content\": [] }");
        return service;
    }

    private static PageBuilderPageController CreateController(
        PublishedRenameContentPreservationTests.FakeGroupedPageService service,
        NoopLinkedComponentReferenceIndexService referenceIndex,
        string requestBody = null,
        IAuthorizationService authorizationService = null)
    {
        var pageService = new PublishedRenameContentPreservationTests.FakePageBuilderPageService(service);
        var controller = new PageBuilderPageController(
            pageService,
            service,
            new PublishedRenameContentPreservationTests.FakeGroupedPageSearchService(),
            authorizationService ?? new PublishedRenameContentPreservationTests.AllowAllAuthorizationService(),
            new PublishedRenameContentPreservationTests.NoopPageDocumentSearchService(),
            referenceIndex,
            new PublishedRenameContentPreservationTests.NoopEventPublisher(),
            NullLogger<PageBuilderPageController>.Instance);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody ?? string.Empty));
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class ThrowingReferenceIndexService : NoopLinkedComponentReferenceIndexService
    {
        public int CallCount { get; private set; }

        public override Task ValidateReferencesForStoreAsync(
            string storeId,
            IEnumerable<string> contents,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidDataException("Linked Component 'missing' was not found.");
        }
    }

    private sealed class StoreScopedAuthorizationService(string allowedStoreId) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            var allowed = resource is GroupedPageBuilderPage page &&
                string.Equals(page.StoreId, allowedStoreId, StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName) => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class DenyPolicyAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName) => Task.FromResult(AuthorizationResult.Failed());
    }

    private const string StoreId = "store";
    private const string ComponentReferenceContent =
        "{ \"settings\": {}, \"content\": [{ \"id\": \"placement\", \"type\": \"componentRef\", \"componentRef\": \"missing\" }] }";
}
