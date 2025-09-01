using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Pages.Core.Search;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-pages")]
[Authorize]
public class PageBuilderPageController : Controller
{
    private readonly IPageBuilderPageService _crudService;
    private readonly IPageBuilderPageSearchService _searchService;
    private readonly ISettingsManager _settingsManager;
    private readonly IGroupedPageService _groupedPageService;
    private readonly IGroupedPageSearchService _groupedPageSearchService;
    private readonly IStoreService _storeService;
    private readonly IAuthorizationService _authorizationService;

    private readonly IPageDocumentSearchService _pageDocumentSearchService;

    public PageBuilderPageController(
        IPageBuilderPageService crudService,
        IPageBuilderPageSearchService searchService,
        ISettingsManager settingsManager,
        IGroupedPageService groupedPageService,
        IGroupedPageSearchService groupedPageSearchService,
        IStoreService storeService,
        IAuthorizationService authorizationService,
        IPageDocumentSearchService pageDocumentSearchService)
    {
        _crudService = crudService;
        _searchService = searchService;
        _settingsManager = settingsManager;
        _groupedPageService = groupedPageService;
        _groupedPageSearchService = groupedPageSearchService;
        _storeService = storeService;
        _authorizationService = authorizationService;
        _pageDocumentSearchService = pageDocumentSearchService;
    }

    [HttpPost("grouped/search")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GroupedPageBuilderPageSearchResult>> SearchGrouped([FromBody] PageBuilderPageSearchCriteria criteria)
    {
        var authorizationResult = await _authorizationService.AuthorizeAsync(User, criteria, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var result = await _groupedPageSearchService.SearchAsync(criteria);
        return Ok(result);
    }

    [HttpGet("grouped/{id}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GroupedPageBuilderPage>> GetGrouped([FromRoute] string id, [FromQuery] string responseGroup = null)
    {
        var groupedPage = await _groupedPageService.GetNoCloneAsync(id, responseGroup);
        if (groupedPage == null)
        {
            return NotFound();
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, groupedPage, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        return Ok(groupedPage);
    }

    [HttpGet("grouped/{id}/edit")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GroupedPageBuilderPage>> GetPageInGroupForEdit([FromRoute] string id, [FromQuery] string responseGroup = null)
    {
        var groupedPage = await _groupedPageService.GetNoCloneAsync(id, responseGroup);
        if (groupedPage == null)
        {
            return NotFound();
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, groupedPage, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var page = groupedPage.PrepareForEdit();

        return Ok(page);
    }

    //[HttpPut("grouped/page")]
    //[Authorize(ModuleConstants.Security.Permissions.Update)]
    //public async Task<ActionResult<PageBuilderPage>> UpdatePage([FromBody] PageBuilderPage model, CancellationToken cancellationToken = default)
    //{
    //    var authorizationResult = await _authorizationService.AuthorizeAsync(User, model, new PageBuilderAuthorizationRequirement());
    //    if (!authorizationResult.Succeeded)
    //    {
    //        return Forbidden;
    //    }

    //    var groupedPage = await _groupedPageService.GetByIdAsync(model.GroupId);

    //    if (groupedPage != null)
    //    {
    //        if (groupedPage.Status == Archived)
    //        {
    //            return BadRequest("Archived page cannot be updated.");
    //        }

    //        // update only draft page, create if it doesn't exist
    //        var draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);
    //        var isNew = draftPage == null;
    //        if (isNew)
    //        {
    //            draftPage = new PageBuilderPage
    //            {
    //                Status = Draft,
    //                GroupId = model.Id,
    //            };
    //            groupedPage.Pages.Add(draftPage);
    //        }

    //        // update draft and grouped page
    //        draftPage.Name = model.Name;
    //        draftPage.Permalink = model.Permalink;
    //        draftPage.CultureName = model.CultureName;
    //        draftPage.StoreId = model.StoreId;
    //        draftPage.Visibility = model.Visibility;
    //        draftPage.UserGroups = model.UserGroups;
    //        draftPage.StartDate = model.StartDate;
    //        draftPage.EndDate = model.EndDate;

    //        await _groupedPageService.SaveChangesAsync([groupedPage]);

    //        var savedGroup = await _groupedPageService.GetNoCloneAsync(model.Id);
    //        if (isNew)
    //        {
    //            var publishedPage = savedGroup.Pages.FirstOrDefault(x => x.Status == Published);
    //            draftPage = savedGroup.Pages.FirstOrDefault(x => x.Status == Draft);

    //            if (publishedPage == null)
    //            {
    //                using var stream = new MemoryStream();
    //                var writer = new StreamWriter(stream);
    //                var defaultContent = new StringBuilder("{ \"settings\": {}, \"content\": [] }");
    //                await writer.WriteLineAsync(defaultContent, cancellationToken);
    //                await writer.FlushAsync(cancellationToken);
    //                stream.Position = 0;
    //                await _groupedPageService.SaveStreamAsContentAsync(draftPage!.Id, stream, cancellationToken);
    //            }
    //            else
    //            {
    //                await _groupedPageService.CopyPageContentAsync(publishedPage.Id, draftPage!.Id, cancellationToken);
    //            }
    //        }

    //        groupedPage = savedGroup;
    //    }

    //    return Ok(groupedPage);
    //}

    /// <summary>
    /// Create or update group for page with status Draft in the given group
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("grouped")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<GroupedPageBuilderPage>> UpdateGrouped([FromBody] GroupedPageBuilderPage model, CancellationToken cancellationToken = default)
    {
        var authorizationResult = await _authorizationService.AuthorizeAsync(User, model, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        // get the existing grouped page for pages Ids
        var groupedPage = await _groupedPageService.GetByIdAsync(model.Id);

        if (groupedPage != null)
        {
            if (groupedPage.Status == Archived)
            {
                return BadRequest("Archived page cannot be updated.");
            }

            // update only draft page, create if it doesn't exist
            var draftPage = groupedPage.Pages.FirstOrDefault(x => x.Status == Draft);
            var isNew = draftPage == null;
            if (isNew)
            {
                draftPage = new PageBuilderPage
                {
                    Status = Draft,
                    GroupId = model.Id,
                };
                groupedPage.Pages.Add(draftPage);
            }

            // update draft and grouped page
            draftPage.Name = model.Name;
            draftPage.Permalink = model.Permalink;
            draftPage.CultureName = model.CultureName;
            draftPage.StoreId = model.StoreId;
            draftPage.Visibility = model.Visibility;
            draftPage.UserGroups = model.UserGroups;
            draftPage.StartDate = model.StartDate;
            draftPage.EndDate = model.EndDate;

            await _groupedPageService.SaveChangesAsync([groupedPage]);

            var savedGroup = await _groupedPageService.GetNoCloneAsync(model.Id);
            if (isNew)
            {
                var publishedPage = savedGroup.Pages.FirstOrDefault(x => x.Status == Published);
                draftPage = savedGroup.Pages.FirstOrDefault(x => x.Status == Draft);

                if (publishedPage == null)
                {
                    using var stream = new MemoryStream();
                    var writer = new StreamWriter(stream);
                    var defaultContent = new StringBuilder("{ \"settings\": {}, \"content\": [] }");
                    await writer.WriteLineAsync(defaultContent, cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                    stream.Position = 0;
                    await _groupedPageService.SaveStreamAsContentAsync(draftPage!.Id, stream, cancellationToken);
                }
                else
                {
                    await _groupedPageService.CopyPageContentAsync(publishedPage.Id, draftPage!.Id, cancellationToken);
                }
            }

            groupedPage = savedGroup;
        }

        return Ok(groupedPage);
    }

    [HttpPost("grouped")]
    [Authorize(ModuleConstants.Security.Permissions.Create)]
    public async Task<ActionResult<GroupedPageBuilderPage>> CreateGrouped([FromBody] GroupedPageBuilderPage model)
    {
        var authorizationResult = await _authorizationService.AuthorizeAsync(User, model, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var groupedPage = new GroupedPageBuilderPage
        {
            Id = null,
            StoreId = model.StoreId,
        };

        var page = new PageBuilderPage
        {
            Id = null,
            Name = model.Name,
            StoreId = model.StoreId,
            CultureName = model.CultureName,
            Permalink = model.Permalink,
            Visibility = model.Visibility,
            UserGroups = model.UserGroups,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
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

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, groupedPages, new PageBuilderAuthorizationRequirement());
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

        await _groupedPageService.SaveChangesAsync(groupedPages);

        return NoContent();
    }

    [HttpPost]
    [Route("grouped/publishing")]
    public async Task<ActionResult> Publishing([FromQuery] string id, [FromQuery] bool publish)
    {
        var groupedPage = await _groupedPageService.GetByIdAsync(id);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, groupedPage, new PageBuilderAuthorizationRequirement());
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

        await _groupedPageService.SaveChangesAsync([groupedPage]);
        await _crudService.DeleteAsync(pagesToDelete);

        return Ok();
    }

    [HttpDelete]
    [Authorize(ModuleConstants.Security.Permissions.Delete)]
    [Route("grouped")]
    public async Task<ActionResult> DeleteGrouped([FromQuery] string id)
    {
        var groupedPages = await _groupedPageService.GetAsync([id]);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, groupedPages, new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        var pageDeleted = false;
        var indexDeleted = false;

        try
        {
            await _groupedPageService.DeleteAsync([id]);
            pageDeleted = true;
        }
        catch { }

        try
        {
            await _pageDocumentSearchService.RemoveDocuments([id]);
            indexDeleted = true;
        }
        catch
        {
        }

        return Ok(new { pageDeleted, indexDeleted });
    }

    [HttpGet]
    [Route("grouped/publish-status")]
    public async Task<ActionResult<FilePublishStatus>> PublishStatus([FromQuery] string id)
    {
        var groupedPage = await _groupedPageService.GetNoCloneAsync(id);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, groupedPage, new PageBuilderAuthorizationRequirement());
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
        var store = await _storeService.GetNoCloneAsync(storeId);
        if (store == null)
        {
            var setting = await _settingsManager.GetObjectSettingAsync(PlatformConstants.Settings.General.Languages.Name);
            return Ok(setting?.AllowedValues ?? []);
        }

        return Ok(store.Languages);
    }

    [HttpGet]
    [Route("user-groups")]
    [Authorize(PlatformConstants.Security.Permissions.SettingQuery)]
    public async Task<ActionResult<string[]>> GetUserGroups()
    {
        var setting = await _settingsManager.GetObjectSettingAsync(CustomerModule.Core.ModuleConstants.Settings.General.MemberGroups.Name);
        return Ok(setting?.AllowedValues ?? []);
    }

    [HttpGet("grouped/content/{pageId}")]
    public async Task GetPageContent([FromRoute] string pageId, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain; charset=utf-8";
        await _groupedPageService.LoadContentToStreamAsync(pageId, Response.Body, cancellationToken);
    }

    [HttpPost("grouped/content/{pageId}")]
    public async Task<IActionResult> SavePageContent([FromRoute] string pageId, CancellationToken cancellationToken)
    {
        await _groupedPageService.SaveStreamAsContentAsync(pageId, Request.Body, cancellationToken);
        return NoContent();
    }

    private static ActionResult Forbidden => new ObjectResult(new { })
    {
        StatusCode = (int)HttpStatusCode.Forbidden,
    };
}
