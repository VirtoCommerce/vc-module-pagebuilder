using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-pages")]
public class PageBuilderPageController : Controller
{
    private readonly IPageBuilderPageService _crudService;
    private readonly IPageBuilderPageSearchService _searchService;
    private readonly ISettingsManager _settingsManager;
    private readonly IGroupedPageService _groupedPageService;
    private readonly IGroupedPageSearchService _groupedPageSearchService;
    private readonly IStoreService _storeService;

    public PageBuilderPageController(
        IPageBuilderPageService crudService,
        IPageBuilderPageSearchService searchService,
        ISettingsManager settingsManager,
        IGroupedPageService groupedPageService,
        IGroupedPageSearchService groupedPageSearchService,
        IStoreService storeService)
    {
        _crudService = crudService;
        _searchService = searchService;
        _settingsManager = settingsManager;
        _groupedPageService = groupedPageService;
        _groupedPageSearchService = groupedPageSearchService;
        _storeService = storeService;
    }

    [HttpPost("grouped/search")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GroupedPageBuilderPageSearchResult>> SearchGrouped([FromBody] PageBuilderPageSearchCriteria criteria)
    {
        var result = await _groupedPageSearchService.SearchAsync(criteria);
        return Ok(result);
    }

    [HttpGet("grouped")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GroupedPageBuilderPage>> GetGrouped([FromQuery] string id, [FromQuery] string responseGroup = null)
    {
        var groupedPage = await _groupedPageService.GetNoCloneAsync(id);
        return Ok(groupedPage);
    }

    [HttpPut("grouped")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<GroupedPageBuilderPage>> UpdateGrouped([FromBody] GroupedPageBuilderPage model)
    {
        // get the existing grouped page for pages Ids
        var groupedPage = await _groupedPageService.GetByIdAsync(model.Id);

        if (groupedPage != null)
        {
            if (groupedPage.Status == Archived)
            {
                return BadRequest("Archived page cannot be updated.");
            }

            // update only draft page, create if doesn't exist
            var draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);
            if (draftPage == null)
            {
                draftPage = new PageBuilderPage
                {
                    Status = Draft,
                    GroupId = model.Id,
                    PageContent = groupedPage.PageContent,
                };
                groupedPage.Pages.Add(draftPage);
            }

            // update draft and grouped page
            groupedPage.Name = draftPage.Name = model.Name;
            groupedPage.Permalink = draftPage.Permalink = model.Permalink;
            groupedPage.CultureName = draftPage.CultureName = model.CultureName;
            groupedPage.StoreId = draftPage.StoreId = model.StoreId;

            await _groupedPageService.SaveChangesAsync([groupedPage]);
        }

        return Ok(groupedPage);
    }

    [HttpPost("grouped")]
    [Authorize(ModuleConstants.Security.Permissions.Create)]
    public async Task<ActionResult<GroupedPageBuilderPage>> CreateGrouped([FromBody] GroupedPageBuilderPage model)
    {
        var groupedPage = new GroupedPageBuilderPage
        {
            Id = null,
            Name = model.Name,
            StoreId = model.StoreId,
            CultureName = model.CultureName,
            Permalink = model.Permalink,
            Status = Draft, // always create a new page in draft status
        };

        var page = new PageBuilderPage
        {
            Id = null,
            Name = model.Name,
            StoreId = model.StoreId,
            CultureName = model.CultureName,
            Permalink = model.Permalink,
            PageContent = JsonConvert.SerializeObject(new { settings = model, content = Array.Empty<string>() }, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            }),
            Status = Draft, // always create a new page in draft status
        };

        groupedPage.Pages.Add(page);

        await _groupedPageService.SaveChangesAsync([groupedPage]);
        return groupedPage;
    }


    [HttpPost("grouped/archive")]
    [Authorize(ModuleConstants.Security.Permissions.Delete)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<ActionResult<GroupedPageBuilderPage>> ArchiveGrouped([FromQuery] string[] ids)
    {
        var groupedPages = await _groupedPageService.GetAsync(ids);

        var pagesToSave = new List<PageBuilderPage>();
        foreach (var groupedPage in groupedPages)
        {
            foreach (var page in groupedPage.Pages)
            {
                page.Status = Archived;
            }
        }

        await _groupedPageService.SaveChangesAsync(groupedPages);

        return NoContent();
    }

    [HttpPost]
    [Route("grouped/publishing")]
    public async Task<ActionResult> Publishing([FromQuery] string id, [FromQuery] bool publish)
    {
        var groupedPage = await _groupedPageService.GetByIdAsync(id);

        var pagesToSave = new List<PageBuilderPage>();
        var pagesToDelete = new List<string>();

        if (publish)
        {
            var pageToPublish = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);
            if (pageToPublish == null)
            {
                return BadRequest("Draft page not found.");
            }

            pageToPublish.Status = Published;
            pagesToSave.Add(pageToPublish);

            pagesToDelete = groupedPage.Pages.Select(x => x.Id).Except(new[] { pageToPublish.Id }).ToList();
        }
        else
        {
            var pageToUnpublish = groupedPage.Pages.FirstOrDefault(x => x.Status == Published);
            if (pageToUnpublish == null)
            {
                return BadRequest("Published page not found.");
            }

            var hasDraft = groupedPage.Pages.Any(x => x.Status == Draft);
            if (hasDraft)
            {
                return BadRequest("Can't unpublish a page that has changes.");
            }

            pageToUnpublish.Status = Draft;
            pagesToSave.Add(pageToUnpublish);

            pagesToDelete = groupedPage.Pages.Select(x => x.Id).Except(new[] { pageToUnpublish.Id }).ToList();
        }

        await _groupedPageService.SaveChangesAsync([groupedPage]);
        await _crudService.DeleteAsync(pagesToDelete.ToArray());

        return Ok();
    }

    [HttpGet]
    [Route("grouped/publish-status")]
    public async Task<ActionResult<FilePublishStatus>> PublishStatus([FromQuery] string id)
    {
        var groupedPage = await _groupedPageService.GetNoCloneAsync(id);

        var result = new FilePublishStatus
        {
            Published = groupedPage.Status == Published,
            HasChanges = groupedPage.HasChanges,
        };

        return Ok(result);
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
        model.Status = Draft; // always create a new page in draft status
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
    public async Task<ActionResult<string[]>> GetAvailableLanguages([FromQuery] string storeId)
    {
        var store = await _storeService.GetByIdAsync(storeId);
        if (store == null)
        {
            var setting = await _settingsManager.GetObjectSettingAsync(PlatformConstants.Settings.General.Languages.Name);
            return Ok(setting?.AllowedValues ?? []);
        }

        return Ok(store.Languages);
    }
}
