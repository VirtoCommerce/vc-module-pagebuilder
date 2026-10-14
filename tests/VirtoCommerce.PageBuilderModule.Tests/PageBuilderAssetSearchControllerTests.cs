using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetSearchControllerTests
{
    [Theory]
    [InlineData(-1, 20, "name")]
    [InlineData(0, 0, "name")]
    [InlineData(0, 101, "name")]
    [InlineData(0, 20, "referencesCount")]
    public async Task Search_RejectsInvalidPagingOrSortBeforeCallingProvider(int skip, int take, string sort)
    {
        var controller = new PageBuilderAssetsController(null, null);
        var response = await controller.Search(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", Skip = skip, Take = take, Sort = sort,
        }, new UnexpectedSearch(), TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public void Search_RequiresTheExistingAssetsReadPermission()
    {
        var attribute = Assert.Single(typeof(PageBuilderAssetsController).GetMethod(nameof(PageBuilderAssetsController.Search))!.GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal(PlatformConstants.Security.Permissions.AssetRead, ((AuthorizeAttribute)attribute).Policy);
    }

    private sealed class UnexpectedSearch : IPageBuilderAssetSearchService
    {
        public Task<PageBuilderAssetSearchResult> SearchAsync(PageBuilderAssetSearchCriteria criteria, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Invalid input must be rejected before the provider is called.");
    }
}
