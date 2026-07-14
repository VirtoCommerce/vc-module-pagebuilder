using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        return Ok(result);
    }

    private static ActionResult Forbidden => new ObjectResult(new { })
    {
        StatusCode = (int)HttpStatusCode.Forbidden,
    };
}
