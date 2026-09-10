using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetsControllerTests
{
    [Fact]
    public async Task SearchReferences_HidesSharedComponentMetadata_WithoutReadPermission()
    {
        var result = CreateSearchResult();
        var controller = CreateController(result, canReadSharedComponents: false);

        var response = await controller.SearchReferences(
            new PageBuilderAssetReferencesSearchCriteria { StoreId = "store" },
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PageBuilderAssetReferencesSearchResult>(ok.Value);
        var reference = Assert.Single(payload.Results);

        Assert.Equal(5, reference.ReferencesCount);
        Assert.Equal(4, reference.PageReferencesCount);
        Assert.Equal(0, reference.SharedComponentReferencesCount);
        Assert.Empty(reference.SharedComponents);
    }

    [Fact]
    public async Task SearchReferences_ReturnsSharedComponentMetadata_WithReadPermission()
    {
        var result = CreateSearchResult();
        var controller = CreateController(result, canReadSharedComponents: true);

        var response = await controller.SearchReferences(
            new PageBuilderAssetReferencesSearchCriteria { StoreId = "store" },
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PageBuilderAssetReferencesSearchResult>(ok.Value);
        var reference = Assert.Single(payload.Results);
        var component = Assert.Single(reference.SharedComponents);

        Assert.Equal(5, reference.ReferencesCount);
        Assert.Equal(1, reference.SharedComponentReferencesCount);
        Assert.Equal("component-1", component.Id);
        Assert.Equal("Shared hero", component.Name);
    }

    private static PageBuilderAssetsController CreateController(
        PageBuilderAssetReferencesSearchResult result,
        bool canReadSharedComponents)
    {
        return new PageBuilderAssetsController(
            new StubAssetReferenceService(result),
            new StubAuthorizationService(canReadSharedComponents))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };
    }

    private static PageBuilderAssetReferencesSearchResult CreateSearchResult()
    {
        return new PageBuilderAssetReferencesSearchResult
        {
            TotalCount = 1,
            Results =
            [
                new PageBuilderAssetReference
                {
                    AssetUrl = "/assets/hero.jpg",
                    ReferencesCount = 5,
                    PageReferencesCount = 4,
                    SharedComponentReferencesCount = 1,
                    SharedComponents =
                    [
                        new PageBuilderAssetReferenceSharedComponent
                        {
                            Id = "component-1",
                            Name = "Shared hero",
                        },
                    ],
                },
            ],
        };
    }

    private sealed class StubAssetReferenceService(PageBuilderAssetReferencesSearchResult result)
        : IPageBuilderAssetReferenceService
    {
        public Task<PageBuilderAssetReferencesSearchResult> SearchReferencesAsync(
            PageBuilderAssetReferencesSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class StubAuthorizationService(bool canReadSharedComponents) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName)
        {
            var authorized = policyName != ModuleConstants.Security.Permissions.SharedComponentsRead ||
                             canReadSharedComponents;
            return Task.FromResult(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }
    }
}
