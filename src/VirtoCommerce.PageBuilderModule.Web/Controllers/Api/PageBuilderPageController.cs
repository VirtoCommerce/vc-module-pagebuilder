using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
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
    IPageDocumentSearchService pageDocumentSearchService,
    IMemberSearchService memberSearchService,
    ILogger<PageBuilderPageController> logger)
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
        groupedPage.OrganizationId = model.OrganizationId;

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
    [Authorize(ModuleConstants.Security.Permissions.Publish)]
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
        catch
        {
            logger.LogDebug($"Couldn't remove group '{groupId}'");
        }

        try
        {
            var pagesIds = groupedPages.SelectMany(x => x.Pages.Select(p => p.Id)).ToArray();
            await pageDocumentSearchService.RemoveDocuments(pagesIds);
            indexDeleted = true;
        }
        catch
        {
            logger.LogDebug($"Couldn't remove pages from group '{groupId}'");
        }

        return Ok(new { pageDeleted, indexDeleted });
    }

    [HttpGet]
    [Route("grouped/publish-status/{groupId}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
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
        var criteria = new MembersSearchCriteria { ObjectIds = [ id ] };
        var result = await memberSearchService.SearchMembersAsync(criteria);
        if (result.TotalCount == 0)
        {
            return NotFound();
        }
        return Ok(result.Results.First());
    }

    [HttpGet("grouped/{groupId}/content")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task GetPageContent([FromRoute] string groupId, [FromQuery] bool draft = true, CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/plain; charset=utf-8";
        var group = await groupedPageService.GetByIdAsync(groupId);
        if (group == null)
        {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, group, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
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
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<IActionResult> SavePageContent([FromRoute] string groupId, CancellationToken cancellationToken = default)
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

        var draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);

        if (draftPage == null)
        {

            draftPage = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
            draftPage.StoreId = groupedPage.StoreId;
            draftPage.Status = Draft;
            groupedPage.Pages.Add(draftPage);
            await groupedPageService.SaveChangesAsync([groupedPage]);

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
