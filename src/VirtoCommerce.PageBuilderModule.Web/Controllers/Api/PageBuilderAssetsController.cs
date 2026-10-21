using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-assets")]
[Authorize]
public class PageBuilderAssetsController(
    IPageBuilderAssetReferenceService assetReferenceService,
    IAuthorizationService authorizationService)
    : Controller
{
    private const int MaximumPageSize = 100;

    [HttpPost("search")]
    [Authorize(PlatformConstants.Security.Permissions.AssetRead)]
    public async Task<ActionResult<PageBuilderAssetSearchResult>> Search(
        [FromBody] PageBuilderAssetSearchCriteria criteria,
        [FromServices] IPageBuilderAssetSearchService assetSearchService,
        CancellationToken cancellationToken = default)
    {
        if (criteria == null || string.IsNullOrWhiteSpace(criteria.FolderUrl))
        {
            return BadRequest("FolderUrl is required.");
        }

        if (criteria.Skip < 0 || criteria.Take is < 1 or > MaximumPageSize)
        {
            return BadRequest($"Skip must be non-negative and Take must be between 1 and {MaximumPageSize}.");
        }

        if (criteria.SortInfos.Any(x => !PageBuilderAssetSort.Columns.ContainsKey(x.SortColumn)))
        {
            return BadRequest($"Supported sort fields: {string.Join(", ", PageBuilderAssetSort.Columns.Keys)}.");
        }

        return Ok(await assetSearchService.SearchAsync(criteria, cancellationToken));
    }

    [HttpPost("references")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<PageBuilderAssetReferencesSearchResult>> SearchReferences(
        [FromBody] PageBuilderAssetReferencesSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria == null || string.IsNullOrWhiteSpace(criteria.StoreId))
        {
            return BadRequest("StoreId is required.");
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, criteria, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var result = await assetReferenceService.SearchReferencesAsync(criteria, cancellationToken);

        if (!await CanReadSharedComponentsAsync())
        {
            foreach (var reference in result.Results)
            {
                // Keep the aggregate reference count so delete preflight remains safe, but do not
                // expose Shared Component metadata through the broader Page Builder read permission.
                reference.SharedComponentReferencesCount = 0;
                reference.SharedComponents = [];
            }
        }

        return Ok(result);
    }

    private async Task<bool> CanReadSharedComponentsAsync()
    {
        var result = await authorizationService.AuthorizeAsync(
            User,
            null,
            ModuleConstants.Security.Permissions.SharedComponentsRead);
        return result.Succeeded;
    }

    private static ActionResult Forbidden => new ObjectResult(new { })
    {
        StatusCode = (int)HttpStatusCode.Forbidden,
    };
}
