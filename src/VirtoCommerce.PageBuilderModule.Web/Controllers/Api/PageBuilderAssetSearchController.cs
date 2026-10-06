using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-assets/search")]
[Authorize(PlatformConstants.Security.Permissions.AssetRead)]
public class PageBuilderAssetSearchController(IPageBuilderAssetSearchService assetSearchService) : Controller
{
    [HttpPost]
    public async Task<ActionResult<BlobEntrySearchResult>> Search(
        [FromBody] PageBuilderAssetSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria == null || string.IsNullOrWhiteSpace(criteria.FolderUrl))
        {
            return BadRequest("FolderUrl is required.");
        }

        if (criteria.Skip < 0 || criteria.Take is < 1 or > 100)
        {
            return BadRequest("Skip must be non-negative and Take must be between 1 and 100.");
        }

        string[] sortableColumns = ["Name", "Type", "Size", "ModifiedDate"];
        if (criteria.SortInfos.Any(x => !sortableColumns.Contains(x.SortColumn, StringComparer.OrdinalIgnoreCase)))
        {
            return BadRequest("Supported sort fields: Name, Type, Size, ModifiedDate.");
        }

        return Ok(await assetSearchService.SearchAsync(criteria, cancellationToken));
    }
}
