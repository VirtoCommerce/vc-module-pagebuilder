using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Models;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderSharedComponentUsageAuthorizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_AlwaysReturnsUsageCount_ButGuardsPageDetails(bool canReadPages)
    {
        var component = new PageBuilderSharedComponent
        {
            Id = "component-1",
            StoreId = "store",
            Name = "Shared hero",
        };
        var usageService = new StubUsageService();
        var controller = new PageBuilderSharedComponentsController(
            new StubSharedComponentService(component),
            sharedComponentSearchService: null,
            sharedComponentContentService: null,
            usageService,
            new StubAuthorizationService(canReadPages))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

        var response = await controller.Get(component.Id, TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PageBuilderSharedComponent>(ok.Value);
        Assert.Equal(2, payload.UsageCount);
        Assert.Equal(canReadPages, usageService.LastIncludePages);

        if (canReadPages)
        {
            Assert.Single(payload.UsagePages);
        }
        else
        {
            Assert.Empty(payload.UsagePages);
        }
    }

    [Fact]
    public async Task Update_WhenComponentWasDeletedAfterAuthorizationRead_ReturnsNotFound()
    {
        var component = new PageBuilderSharedComponent
        {
            Id = "component-1",
            StoreId = "store",
            Name = "Shared hero",
        };
        var componentService = new StubSharedComponentService(component)
        {
            MetadataUpdateResult = null,
        };
        var controller = new PageBuilderSharedComponentsController(
            componentService,
            sharedComponentSearchService: null,
            sharedComponentContentService: null,
            new StubUsageService(),
            new StubAuthorizationService(canReadPages: true))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

        var response = await controller.Update(
            component.Id,
            new PageBuilderSharedComponentUpdateModel { Name = "Renamed hero" },
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task GetContent_WhenAuthorizedComponentWasReplaced_ReturnsNotFoundWithoutReadingById()
    {
        var component = CreateAuthorizedComponent();
        var contentService = new StubSharedComponentContentService
        {
            ConditionalLoadResult = null,
        };
        var controller = CreateController(
            new StubSharedComponentService(component),
            contentService,
            new StubUsageService());

        var response = await controller.GetContent(
            component.Id,
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(response);
        Assert.Same(component, contentService.LastExpectedComponent);
        Assert.False(contentService.LegacyLoadCalled);
    }

    [Fact]
    public async Task SaveContent_WhenAuthorizedComponentWasReplaced_ReturnsNotFoundWithoutWritingById()
    {
        var component = CreateAuthorizedComponent();
        var contentService = new StubSharedComponentContentService
        {
            ConditionalSaveResult = false,
        };
        var controller = CreateController(
            new StubSharedComponentService(component),
            contentService,
            new StubUsageService());

        var response = await controller.SaveContent(
            component.Id,
            JObject.Parse("{ \"settings\": {}, \"content\": [] }"),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(response);
        Assert.Same(component, contentService.LastExpectedComponent);
        Assert.False(contentService.LegacySaveCalled);
    }

    [Fact]
    public async Task Delete_WhenAuthorizedComponentWasReplaced_ReturnsNotFoundWithoutDeletingById()
    {
        var component = CreateAuthorizedComponent();
        var componentService = new StubSharedComponentService(component)
        {
            ConditionalDeleteResult = false,
        };
        var controller = CreateController(
            componentService,
            new StubSharedComponentContentService(),
            new StubUsageService(usageCount: 0));

        var response = await controller.Delete(
            component.Id,
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(response);
        Assert.Same(component, componentService.LastDeleteExpectedComponent);
        Assert.False(componentService.LegacyDeleteCalled);
    }

    private static PageBuilderSharedComponent CreateAuthorizedComponent()
    {
        return new PageBuilderSharedComponent
        {
            Id = "component-1",
            StoreId = "store",
            Name = "Shared hero",
            CreatedDate = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc),
        };
    }

    private static PageBuilderSharedComponentsController CreateController(
        IPageBuilderSharedComponentService componentService,
        IPageBuilderSharedComponentContentService contentService,
        IPageBuilderSharedComponentUsageService usageService)
    {
        return new PageBuilderSharedComponentsController(
            componentService,
            sharedComponentSearchService: null,
            contentService,
            usageService,
            new StubAuthorizationService(canReadPages: true))
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

    private sealed class StubSharedComponentService(PageBuilderSharedComponent component)
        : IPageBuilderSharedComponentService
    {
        public PageBuilderSharedComponent MetadataUpdateResult { get; set; }
        public bool ConditionalDeleteResult { get; set; }
        public PageBuilderSharedComponent LastDeleteExpectedComponent { get; private set; }
        public bool LegacyDeleteCalled { get; private set; }

        public Task<IList<PageBuilderSharedComponent>> GetAsync(
            IList<string> ids,
            string responseGroup = null,
            bool clone = true)
        {
            IList<PageBuilderSharedComponent> result = ids.Contains(component.Id) ? [component] : [];
            return Task.FromResult(result);
        }

        public Task SaveChangesAsync(IList<PageBuilderSharedComponent> models)
        {
            throw new System.NotSupportedException();
        }

        public Task<PageBuilderSharedComponent> UpdateMetadataAsync(
            PageBuilderSharedComponent model,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MetadataUpdateResult);
        }

        public Task SaveWithContentAsync(
            PageBuilderSharedComponent model,
            string content,
            CancellationToken cancellationToken = default)
        {
            throw new System.NotSupportedException();
        }

        public Task<bool> TryDeleteAsync(
            PageBuilderSharedComponent expectedComponent,
            CancellationToken cancellationToken = default)
        {
            LastDeleteExpectedComponent = expectedComponent;
            return Task.FromResult(ConditionalDeleteResult);
        }

        public Task DeleteAsync(IList<string> ids, bool softDelete = false)
        {
            LegacyDeleteCalled = true;
            throw new System.NotSupportedException();
        }
    }

    private sealed class StubSharedComponentContentService : IPageBuilderSharedComponentContentService
    {
        public string ConditionalLoadResult { get; set; }
        public bool ConditionalSaveResult { get; set; }
        public PageBuilderSharedComponent LastExpectedComponent { get; private set; }
        public bool LegacyLoadCalled { get; private set; }
        public bool LegacySaveCalled { get; private set; }

        public Task<string> LoadContentAsync(
            string sharedComponentId,
            CancellationToken cancellationToken = default)
        {
            LegacyLoadCalled = true;
            throw new System.NotSupportedException();
        }

        public Task<string> TryLoadContentAsync(
            PageBuilderSharedComponent expectedComponent,
            CancellationToken cancellationToken = default)
        {
            LastExpectedComponent = expectedComponent;
            return Task.FromResult(ConditionalLoadResult);
        }

        public Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
            IEnumerable<string> sharedComponentIds,
            CancellationToken cancellationToken = default)
        {
            throw new System.NotSupportedException();
        }

        public Task SaveContentAsync(
            string sharedComponentId,
            string content,
            CancellationToken cancellationToken = default)
        {
            LegacySaveCalled = true;
            throw new System.NotSupportedException();
        }

        public Task<bool> TrySaveContentAsync(
            PageBuilderSharedComponent expectedComponent,
            string content,
            CancellationToken cancellationToken = default)
        {
            LastExpectedComponent = expectedComponent;
            return Task.FromResult(ConditionalSaveResult);
        }
    }

    private sealed class StubUsageService(int usageCount = 2) : IPageBuilderSharedComponentUsageService
    {
        public bool LastIncludePages { get; private set; }

        public Task<IList<PageBuilderSharedComponentUsage>> GetUsageAsync(
            IEnumerable<string> sharedComponentIds,
            string storeId,
            bool includePages = true,
            CancellationToken cancellationToken = default)
        {
            LastIncludePages = includePages;
            IList<PageBuilderSharedComponentUsage> result =
            [
                new PageBuilderSharedComponentUsage
                {
                    SharedComponentId = "component-1",
                    UsageCount = usageCount,
                    // The controller must not trust service implementations to strip protected details.
                    Pages =
                    [
                        new PageBuilderSharedComponentUsagePage
                        {
                            Id = "page-1",
                            Name = "Homepage",
                            Permalink = "homepage",
                            CultureName = "en-US",
                            Status = "Draft",
                        },
                    ],
                },
            ];
            return Task.FromResult(result);
        }
    }

    private sealed class StubAuthorizationService(bool canReadPages) : IAuthorizationService
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
            var authorized = policyName != ModuleConstants.Security.Permissions.Read || canReadPages;
            return Task.FromResult(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }
    }
}
