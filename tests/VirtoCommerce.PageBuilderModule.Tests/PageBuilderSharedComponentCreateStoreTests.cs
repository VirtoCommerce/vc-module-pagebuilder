using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderSharedComponentCreateStoreTests
{
    [Theory]
    [InlineData("NO-SUCH-STORE-XYZ")]
    [InlineData(" store ")]
    public async Task Create_UnknownStore_ReturnsBadRequestWithoutSaving(string storeId)
    {
        var stores = new FakeStoreService(new Store { Id = "store" });
        var components = new RecordingComponentService();
        var controller = CreateController(stores, components);

        var response = await controller.Create(Request(storeId), TestContext.Current.CancellationToken);

        var error = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal($"Store '{storeId}' does not exist.", error.Value);
        Assert.Equal(storeId, stores.LastRequestedId);
        Assert.False(stores.LastClone);
        Assert.Null(components.SavedModel);
        Assert.Null(components.SavedContent);
    }

    [Fact]
    public async Task Create_ExistingStore_CreatesWithContentAndForwardsCancellation()
    {
        var stores = new FakeStoreService(new Store { Id = "store" });
        var components = new RecordingComponentService();
        var controller = CreateController(stores, components);
        var request = Request("store");
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await controller.Create(request, cancellationToken);

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        var model = Assert.IsType<PageBuilderSharedComponent>(created.Value);
        Assert.Equal(nameof(PageBuilderSharedComponentsController.Get), created.ActionName);
        Assert.Equal("created-component", created.RouteValues["id"]);
        Assert.Same(components.SavedModel, model);
        Assert.Equal("store", model.StoreId);
        Assert.Equal(request.Name, model.Name);
        Assert.Equal(request.Content.ToString(Formatting.None), components.SavedContent);
        Assert.Equal(cancellationToken, components.CancellationToken);
    }

    [Fact]
    public async Task Create_UnauthorizedStore_ReturnsForbiddenBeforeStoreLookupOrSave()
    {
        var stores = new FakeStoreService(null);
        var components = new RecordingComponentService();
        var controller = CreateController(stores, components, authorized: false);

        var response = await controller.Create(Request("foreign-store"), TestContext.Current.CancellationToken);

        var forbidden = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Null(stores.LastRequestedId);
        Assert.Null(components.SavedModel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Create_MissingStoreId_ReturnsBadRequestWithoutLookupOrSave(string storeId)
    {
        var stores = new FakeStoreService(null);
        var components = new RecordingComponentService();
        var controller = CreateController(stores, components);

        var response = await controller.Create(Request(storeId), TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(stores.LastRequestedId);
        Assert.Null(components.SavedModel);
    }

    [Fact]
    public async Task Create_PublishedConstructor_ResolvesStoreServiceFromRequestServices()
    {
        var stores = new FakeStoreService(new Store { Id = "store" });
        var components = new RecordingComponentService();
        using var services = new ServiceCollection().AddSingleton<IStoreService>(stores).BuildServiceProvider();
        var controller = new PageBuilderSharedComponentsController(components, null, null, null,
            new FakeAuthorizationService(true))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
            },
        };

        var response = await controller.Create(Request("store"), TestContext.Current.CancellationToken);

        Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.False(stores.LastClone);
    }

    private static PageBuilderSharedComponentCreateModel Request(string storeId) => new()
    {
        StoreId = storeId,
        Name = "Shared hero",
        Content = JObject.Parse("""{"settings":{},"content":[{"id":"hero","type":"text"}]}"""),
    };

    private static PageBuilderSharedComponentsController CreateController(
        IStoreService stores, IPageBuilderSharedComponentService components, bool authorized = true) =>
        new(components, null, null, null, new FakeAuthorizationService(authorized), stores)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

    private sealed class RecordingComponentService : IPageBuilderSharedComponentService
    {
        public PageBuilderSharedComponent SavedModel { get; private set; }
        public string SavedContent { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task SaveWithContentAsync(PageBuilderSharedComponent model, string content, CancellationToken cancellationToken = default)
        {
            SavedModel = model;
            SavedContent = content;
            CancellationToken = cancellationToken;
            model.Id = "created-component";
            return Task.CompletedTask;
        }

        public Task<IList<PageBuilderSharedComponent>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true) =>
            throw new NotSupportedException();
        public Task SaveChangesAsync(IList<PageBuilderSharedComponent> models) => throw new NotSupportedException();
        public Task<PageBuilderSharedComponent> UpdateMetadataAsync(PageBuilderSharedComponent model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> TryDeleteAsync(PageBuilderSharedComponent expectedComponent, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
    }

}
