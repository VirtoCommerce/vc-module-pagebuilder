using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-pages")]
[Authorize]
public class PageBuilderPageSettingsController(
    ISettingsManager settingsManager,
    IStoreService storeService,
    IMemberSearchService memberSearchService)
    : Controller
{
    [HttpGet]
    [Route("languages")]
    [Authorize(PlatformConstants.Security.Permissions.SettingQuery)]
    public async Task<ActionResult<string[]>> GetAvailableLanguages([FromQuery] string storeId)
    {
        var store = await storeService.GetNoCloneAsync(storeId);
        if (store == null)
        {
            var setting = await settingsManager.GetObjectSettingAsync(PlatformConstants.Settings.General.Languages.Name);
            return Ok(setting?.AllowedValues ?? []);
        }

        return Ok(store.Languages);
    }

    [HttpGet]
    [Route("user-groups")]
    [Authorize(PlatformConstants.Security.Permissions.SettingQuery)]
    public async Task<ActionResult<string[]>> GetUserGroups()
    {
        var setting = await settingsManager.GetObjectSettingAsync(CustomerModule.Core.ModuleConstants.Settings.General.MemberGroups.Name);
        return Ok(setting?.AllowedValues ?? []);
    }

    [HttpPost]
    [Route("organizations")]
    [Authorize(CustomerModule.Core.ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<MemberSearchResult>> GetOrganizations([FromBody] MembersSearchCriteria criteria)
    {
        criteria.MemberType = nameof(Organization);
        criteria.DeepSearch = true;
        var result = await memberSearchService.SearchMembersAsync(criteria);
        return Ok(result);
    }

    [HttpGet]
    [Route("organizations/{id}")]
    [Authorize(CustomerModule.Core.ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<Member>> GetOrganization([FromRoute] string id)
    {
        var criteria = new MembersSearchCriteria { ObjectIds = [id] };
        var result = await memberSearchService.SearchMembersAsync(criteria);
        if (result.TotalCount == 0)
        {
            return NotFound();
        }
        return Ok(result.Results.First());
    }
}
