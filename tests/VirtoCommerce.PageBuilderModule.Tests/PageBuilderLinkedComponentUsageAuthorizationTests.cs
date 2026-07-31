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

public class PageBuilderLinkedComponentUsageAuthorizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_AlwaysReturnsUsageCount_ButGuardsPageDetails(bool canReadPages)
    {
        var component = new PageBuilderLinkedComponent
        {
            Id = "component-1",
            StoreId = "store",
            Name = "Shared hero",
        };
        var usageService = new StubUsageService();
        var controller = new PageBuilderLinkedComponentsController(
            new StubLinkedComponentService(component),
            linkedComponentSearchService: null,
            linkedComponentContentService: null,
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
        var payload = Assert.IsType<PageBuilderLinkedComponent>(ok.Value);
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
        var component = new PageBuilderLinkedComponent
        {
            Id = "component-1",
            StoreId = "store",
            Name = "Shared hero",
        };
        var componentService = new StubLinkedComponentService(component)
        {
            MetadataUpdateResult = null,
        };
        var controller = new PageBuilderLinkedComponentsController(
            componentService,
            linkedComponentSearchService: null,
            linkedComponentContentService: null,
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
            new PageBuilderLinkedComponentUpdateModel { Name = "Renamed hero" },
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task GetContent_WhenAuthorizedComponentWasReplaced_ReturnsNotFoundWithoutReadingById()
    {
        var component = CreateAuthorizedComponent();
        var contentService = new StubLinkedComponentContentService
        {
            ConditionalLoadResult = null,
        };
        var controller = CreateController(
            new StubLinkedComponentService(component),
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
        var contentService = new StubLinkedComponentContentService
        {
            ConditionalSaveResult = false,
        };
        var controller = CreateController(
            new StubLinkedComponentService(component),
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
        var componentService = new StubLinkedComponentService(component)
        {
            ConditionalDeleteResult = false,
        };
        var controller = CreateController(
            componentService,
            new StubLinkedComponentContentService(),
            new StubUsageService(usageCount: 0));

        var response = await controller.Delete(
            component.Id,
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(response);
        Assert.Same(component, componentService.LastDeleteExpectedComponent);
        Assert.False(componentService.LegacyDeleteCalled);
    }

    private static PageBuilderLinkedComponent CreateAuthorizedComponent()
    {
        return new PageBuilderLinkedComponent
        {
            Id = "component-1",
            StoreId = "store",
            Name = "Shared hero",
            CreatedDate = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc),
        };
    }

    private static PageBuilderLinkedComponentsController CreateController(
        IPageBuilderLinkedComponentService componentService,
        IPageBuilderLinkedComponentContentService contentService,
        IPageBuilderLinkedComponentUsageService usageService)
    {
        return new PageBuilderLinkedComponentsController(
            componentService,
            linkedComponentSearchService: null,
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

    private sealed class StubLinkedComponentService(PageBuilderLinkedComponent component)
        : IPageBuilderLinkedComponentService
    {
        public PageBuilderLinkedComponent MetadataUpdateResult { get; set; }
        public bool ConditionalDeleteResult { get; set; }
        public PageBuilderLinkedComponent LastDeleteExpectedComponent { get; private set; }
        public bool LegacyDeleteCalled { get; private set; }

        public Task<IList<PageBuilderLinkedComponent>> GetAsync(
            IList<string> ids,
            string responseGroup = null,
            bool clone = true)
        {
            IList<PageBuilderLinkedComponent> result = ids.Contains(component.Id) ? [component] : [];
            return Task.FromResult(result);
        }

        public Task SaveChangesAsync(IList<PageBuilderLinkedComponent> models)
        {
            throw new System.NotSupportedException();
        }

        public Task<PageBuilderLinkedComponent> UpdateMetadataAsync(
            PageBuilderLinkedComponent model,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MetadataUpdateResult);
        }

        public Task SaveWithContentAsync(
            PageBuilderLinkedComponent model,
            string content,
            CancellationToken cancellationToken = default)
        {
            throw new System.NotSupportedException();
        }

        public Task<bool> TryDeleteAsync(
            PageBuilderLinkedComponent expectedComponent,
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

    private sealed class StubLinkedComponentContentService : IPageBuilderLinkedComponentContentService
    {
        public string ConditionalLoadResult { get; set; }
        public bool ConditionalSaveResult { get; set; }
        public PageBuilderLinkedComponent LastExpectedComponent { get; private set; }
        public bool LegacyLoadCalled { get; private set; }
        public bool LegacySaveCalled { get; private set; }

        public Task<string> LoadContentAsync(
            string linkedComponentId,
            CancellationToken cancellationToken = default)
        {
            LegacyLoadCalled = true;
            throw new System.NotSupportedException();
        }

        public Task<string> TryLoadContentAsync(
            PageBuilderLinkedComponent expectedComponent,
            CancellationToken cancellationToken = default)
        {
            LastExpectedComponent = expectedComponent;
            return Task.FromResult(ConditionalLoadResult);
        }

        public Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
            IEnumerable<string> linkedComponentIds,
            CancellationToken cancellationToken = default)
        {
            throw new System.NotSupportedException();
        }

        public Task SaveContentAsync(
            string linkedComponentId,
            string content,
            CancellationToken cancellationToken = default)
        {
            LegacySaveCalled = true;
            throw new System.NotSupportedException();
        }

        public Task<bool> TrySaveContentAsync(
            PageBuilderLinkedComponent expectedComponent,
            string content,
            CancellationToken cancellationToken = default)
        {
            LastExpectedComponent = expectedComponent;
            return Task.FromResult(ConditionalSaveResult);
        }
    }

    private sealed class StubUsageService(int usageCount = 2) : IPageBuilderLinkedComponentUsageService
    {
        public bool LastIncludePages { get; private set; }

        public Task<IList<PageBuilderLinkedComponentUsage>> GetUsageAsync(
            IEnumerable<string> linkedComponentIds,
            string storeId,
            bool includePages = true,
            CancellationToken cancellationToken = default)
        {
            LastIncludePages = includePages;
            IList<PageBuilderLinkedComponentUsage> result =
            [
                new PageBuilderLinkedComponentUsage
                {
                    LinkedComponentId = "component-1",
                    UsageCount = usageCount,
                    // The controller must not trust service implementations to strip protected details.
                    Pages =
                    [
                        new PageBuilderLinkedComponentUsagePage
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
