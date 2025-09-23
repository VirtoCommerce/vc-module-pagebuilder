using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.Pages.Core.Search;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-pages")]
[Authorize]
public class PageBuilderPageController(
    IPageBuilderPageService crudService,
    ISettingsManager settingsManager,
    IGroupedPageService groupedPageService,
    IGroupedPageSearchService groupedPageSearchService,
    IStoreService storeService,
    IAuthorizationService authorizationService,
    IPageDocumentSearchService pageDocumentSearchService)
    : Controller
{
    [HttpPost("search")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GroupedPageBuilderPageSearchResult>> SearchGroups([FromBody] PageBuilderPageSearchCriteria criteria)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, criteria, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var result = await groupedPageSearchService.SearchAsync(criteria);

        return Ok(result);
    }

    [HttpGet("grouped/{groupId}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GroupedPageBuilderPage>> GetGroup([FromRoute] string groupId, [FromQuery] string responseGroup = null)
    {
        var groupedPage = await groupedPageService.GetNoCloneAsync(groupId, responseGroup);
        if (groupedPage == null)
        {
            return NotFound();
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, groupedPage, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        return Ok(groupedPage);
    }

    /// <summary>
    /// Create or update group for page with status Draft in the given group
    /// </summary>
    /// <param name="model">Model of page to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    [HttpPut("grouped")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<GroupedPageBuilderPage>> UpdateGroup([FromBody] GroupedPageBuilderPage model, CancellationToken cancellationToken = default)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, model, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        // get the existing grouped page for pages Ids
        var groupedPage = await groupedPageService.GetByIdAsync(model.Id);

        var newGroup = false;

        if (groupedPage == null)
        {
            groupedPage = AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance();
            var draftPage = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
            draftPage.Status = Draft;
            draftPage.StoreId = model.StoreId;
            groupedPage.Pages.Add(draftPage);
            newGroup = true;
        }
        else if (groupedPage.Status == Archived)
        {
            return BadRequest("Archived page cannot be updated.");
        }

        groupedPage.Name = model.Name;
        groupedPage.Permalink = model.Permalink;
        groupedPage.CultureName = model.CultureName;
        groupedPage.StoreId = model.StoreId;
        groupedPage.Visibility = model.Visibility;
        groupedPage.UserGroups = model.UserGroups;
        groupedPage.StartDate = model.StartDate;
        groupedPage.EndDate = model.EndDate;

        await groupedPageService.SaveChangesAsync([groupedPage]);
        if (newGroup)
        {
            await WriteDefaultContent(groupedPage.Pages.First().Id, cancellationToken);
        }

        return Ok(groupedPage);
    }

    [HttpPost("grouped")]
    [Authorize(ModuleConstants.Security.Permissions.Create)]
    public async Task<ActionResult<GroupedPageBuilderPage>> CreateGroup([FromBody] GroupedPageBuilderPage model, CancellationToken cancellationToken = default)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, model, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var draftPage = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();

        draftPage.Id = null;
        draftPage.Status = Draft; // always create a new page in draft status
        draftPage.StoreId = model.StoreId;

        model.Pages.Add(draftPage);

        await groupedPageService.SaveChangesAsync([model]);
        await WriteDefaultContent(draftPage.Id, cancellationToken);

        return Ok(model);
    }

    [HttpPost("grouped/archive")]
    [Authorize(ModuleConstants.Security.Permissions.Delete)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ArchiveGroups([FromQuery] string[] ids, CancellationToken cancellationToken = default)
    {
        var groupedPages = await groupedPageService.GetAsync(ids);

        var authorizationResult = await authorizationService.AuthorizeAsync(User, groupedPages, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        foreach (var groupedPage in groupedPages)
        {
            foreach (var page in groupedPage.Pages)
            {
                page.Status = Archived;
            }
        }

        await groupedPageService.SaveChangesAsync(groupedPages);

        return NoContent();
    }

    [HttpPost]
    [Route("grouped/publishing/{groupId}")]
    public async Task<ActionResult> PublishGroup([FromRoute] string groupId, [FromQuery] bool publish, CancellationToken cancellationToken = default)
    {
        var groupedPage = await groupedPageService.GetByIdAsync(groupId);

        if (groupedPage == null)
        {
            return NotFound();
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, groupedPage, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        List<string> pagesToDelete;

        if (publish)
        {
            var pageToPublish = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);
            if (pageToPublish == null)
            {
                return BadRequest("Draft page not found.");
            }

            pageToPublish.Status = Published;
            pagesToDelete = groupedPage.Pages.Where(x => x.Id != pageToPublish.Id).Select(x => x.Id).ToList();
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
            pagesToDelete = groupedPage.Pages.Where(x => x.Id != pageToUnpublish.Id).Select(x => x.Id).ToList();
        }

        await groupedPageService.SaveChangesAsync([groupedPage]);
        await crudService.DeleteAsync(pagesToDelete);

        return Ok();
    }

    [HttpDelete]
    [Authorize(ModuleConstants.Security.Permissions.Delete)]
    [Route("grouped/{groupId}")]
    public async Task<ActionResult> DeleteGroup([FromRoute] string groupId)
    {
        var groupedPages = await groupedPageService.GetAsync([groupId]);

        var authorizationResult = await authorizationService.AuthorizeAsync(User, groupedPages, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var pageDeleted = false;
        var indexDeleted = false;

        try
        {
            await groupedPageService.DeleteAsync([groupId]);
            pageDeleted = true;
        }
        catch { } // todo: what i need to do here?

        try
        {
            var pagesIds = groupedPages.SelectMany(x => x.Pages.Select(p => p.Id)).ToArray();
            await pageDocumentSearchService.RemoveDocuments(pagesIds);
            indexDeleted = true;
        }
        catch { } // todo: what i need to do here?

        return Ok(new { pageDeleted, indexDeleted });
    }

    [HttpGet]
    [Route("grouped/publish-status/{groupId}")]
    public async Task<ActionResult<FilePublishStatus>> PublishStatus([FromRoute] string groupId)
    {
        var groupedPage = await groupedPageService.GetNoCloneAsync(groupId);

        var authorizationResult = await authorizationService.AuthorizeAsync(User, groupedPage, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var result = new FilePublishStatus
        {
            Published = groupedPage.Status == Published,
            HasChanges = groupedPage.HasChanges,
        };

        return Ok(result);
    }

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

    [HttpGet("grouped/{groupId}/content")]
    public async Task GetPageContent([FromRoute] string groupId, [FromQuery] bool draft = true, CancellationToken cancellationToken = default)
    {
        // todo: check auth
        Response.ContentType = "text/plain; charset=utf-8";
        var group = await groupedPageService.GetByIdAsync(groupId);
        if (group == null)
        {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        var pageId = group.Pages.Where(x => (draft && x.Status == Draft) || x.Status == Published)
            .OrderByDescending(x => x.ModifiedDate).Select(x => x.Id).FirstOrDefault();
        if (pageId == null)
        {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }
        await groupedPageService.LoadContentToStreamAsync(pageId, Response.Body, cancellationToken);
    }

    [HttpPost("grouped/{groupId}/content")]
    public async Task<IActionResult> SavePageContent([FromRoute] string groupId, CancellationToken cancellationToken = default)
    {
        var groupedPage = await groupedPageService.GetByIdAsync(groupId);

        // todo: check auth

        if (groupedPage == null)
        {
            return NotFound();
        }

        var draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);

        if (draftPage == null)
        {

            draftPage = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
            draftPage.StoreId = groupedPage.StoreId;
            draftPage.Status = Draft;
            groupedPage.Pages.Add(draftPage);
            await groupedPageService.SaveChangesAsync([groupedPage]);

            // todo: how to get page id?
            groupedPage = await groupedPageService.GetByIdAsync(groupId);
            draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);
        }

        var pageId = draftPage!.Id;
        await groupedPageService.SaveStreamAsContentAsync(pageId, Request.Body, cancellationToken);

        return NoContent();
    }

    private static ActionResult Forbidden => new ObjectResult(new { })
    {
        StatusCode = (int)HttpStatusCode.Forbidden,
    };

    private async Task WriteDefaultContent(string pageId, CancellationToken cancellationToken)
    {
        using var stream = new System.IO.MemoryStream();
        var writer = new System.IO.StreamWriter(stream);
        await writer.WriteAsync(ModuleConstants.DefaultPageContent.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        stream.Position = 0;

        await groupedPageService.SaveStreamAsContentAsync(pageId, stream, cancellationToken);

    }
}
