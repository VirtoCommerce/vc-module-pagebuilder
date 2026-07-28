using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

        if (!await CanReadLinkedComponentsAsync())
        {
            foreach (var reference in result.Results)
            {
                // Keep the aggregate reference count so delete preflight remains safe, but do not
                // expose Shared Component metadata through the broader Page Builder read permission.
                reference.LinkedComponentReferencesCount = 0;
                reference.LinkedComponents = [];
            }
        }

        return Ok(result);
    }

    private async Task<bool> CanReadLinkedComponentsAsync()
    {
        var result = await authorizationService.AuthorizeAsync(
            User,
            null,
            ModuleConstants.Security.Permissions.LinkedComponentsRead);
        return result.Succeeded;
    }

    private static ActionResult Forbidden => new ObjectResult(new { })
    {
        StatusCode = (int)HttpStatusCode.Forbidden,
    };
}
