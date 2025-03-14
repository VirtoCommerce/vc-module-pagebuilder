using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-pages")]
public class PageBuilderPageController : Controller
{
    private readonly IPageBuilderPageService _crudService;
    private readonly IPageBuilderPageSearchService _searchService;
    private readonly ISettingsManager _settingsManager;

    public PageBuilderPageController(
        IPageBuilderPageService crudService,
        IPageBuilderPageSearchService searchService,
        ISettingsManager settingsManager)
    {
        _crudService = crudService;
        _searchService = searchService;
        _settingsManager = settingsManager;
    }

    [HttpPost("search")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<PageBuilderPageSearchResult>> Search([FromBody] PageBuilderPageSearchCriteria criteria)
    {
        var result = await _searchService.SearchNoCloneAsync(criteria);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(ModuleConstants.Security.Permissions.Create)]
    public Task<ActionResult<PageBuilderPage>> Create([FromBody] PageBuilderPage model)
    {
        model.Id = null;
        return Update(model);
    }

    [HttpPut]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<PageBuilderPage>> Update([FromBody] PageBuilderPage model)
    {
        await _crudService.SaveChangesAsync([model]);
        return Ok(model);
    }

    [HttpGet("{id}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<PageBuilderPage>> Get([FromRoute] string id, [FromQuery] string responseGroup = null)
    {
        var model = await _crudService.GetNoCloneAsync(id, responseGroup);
        return Ok(model);
    }

    [HttpDelete]
    [Authorize(ModuleConstants.Security.Permissions.Delete)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete([FromQuery] string[] ids)
    {
        await _crudService.DeleteAsync(ids);
        return NoContent();
    }

    [HttpGet]
    [Route("languages")]
    public async Task<ActionResult<string[]>> GetAvailableLanguages()
    {
        var setting = await _settingsManager.GetObjectSettingAsync(PlatformConstants.Settings.General.Languages.Name);
        return Ok(setting?.AllowedValues ?? []);
    }
}
