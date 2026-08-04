using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Pages.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-pages")]
[Authorize]
public class PageBuilderPageController : Controller
{
    private readonly IPageBuilderPageService crudService;
    private readonly IGroupedPageService groupedPageService;
    private readonly IGroupedPageSearchService groupedPageSearchService;
    private readonly IAuthorizationService authorizationService;
    private readonly IPageDocumentSearchService pageDocumentSearchService;
    private readonly PageBuilderPageContentService pageContentService;
    private readonly ILogger<PageBuilderPageController> logger;

#pragma warning disable S107 // Preserve the published controller constructor for source and binary compatibility.
    public PageBuilderPageController(
        IPageBuilderPageService crudService,
        IGroupedPageService groupedPageService,
        IGroupedPageSearchService groupedPageSearchService,
        IAuthorizationService authorizationService,
        IPageDocumentSearchService pageDocumentSearchService,
        IPageBuilderSharedComponentReferenceIndexService sharedComponentReferenceIndexService,
        IEventPublisher eventPublisher,
        ILogger<PageBuilderPageController> logger)
    {
        this.crudService = crudService;
        this.groupedPageService = groupedPageService;
        this.groupedPageSearchService = groupedPageSearchService;
        this.authorizationService = authorizationService;
        this.pageDocumentSearchService = pageDocumentSearchService;
        pageContentService = new PageBuilderPageContentService(
            crudService,
            groupedPageService,
            sharedComponentReferenceIndexService,
            eventPublisher,
            logger);
        this.logger = logger;
    }
#pragma warning restore S107

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
        var groupedPage = await groupedPageService.GetByIdAsync(model.Id);
        var storeChanged = HasStoreChanged(groupedPage, model);
        var authorizationError = await AuthorizeGroupUpdateAsync(groupedPage, model, storeChanged);
        if (authorizationError != null)
        {
            return authorizationError;
        }

        if (groupedPage?.Status == Archived)
        {
            return BadRequest("Archived page cannot be updated.");
        }

        groupedPage ??= AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance();
        ApplyGroupChanges(groupedPage, model, storeChanged);

        var sourcePageId = GetPublishedContentSourceId(groupedPage);
        if (sourcePageId != null)
        {
            var sourceContent = await groupedPageService.LoadContent(sourcePageId, cancellationToken);
            var validationError = await ValidateSharedComponentContentAsync(
                groupedPage.StoreId,
                sourceContent,
                cancellationToken);
            if (validationError != null)
            {
                return validationError;
            }
        }

        var writeResult = await pageContentService.SaveGroupUpdateAsync(
            groupedPage,
            sourcePageId,
            cancellationToken);
        if (writeResult.ErrorMessage != null)
        {
            return BadRequest(writeResult.ErrorMessage);
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
        await pageContentService.UpdateGroupSettingsAsync(draftPage.Id, model, cancellationToken);

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
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Couldn't remove group '{GroupId}'", groupId);
        }

        try
        {
            var pagesIds = groupedPages.SelectMany(x => x.Pages.Select(p => p.Id)).ToArray();
            await pageDocumentSearchService.RemoveDocuments(pagesIds);
            indexDeleted = true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Couldn't remove pages from group '{GroupId}'", groupId);
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

        // Walk the candidates in order of authority and serve the first one that actually has content.
        // Picking by newest ModifiedDate instead used to hand out the wrong page: a draft that UpdateGroup had
        // just created but not yet seeded is the newest row while its content is still NULL, and pages demoted
        // to Archived by NormalizePublishedPages share the Published page's timestamp, so the winner of that
        // comparison was undefined. Falling through on "no content" also covers a stale cached group that still
        // lists deleted pages.
        foreach (var pageId in GetContentCandidates(group, draft))
        {
            if (await groupedPageService.LoadContentToStreamAsync(pageId, Response.Body, cancellationToken))
            {
                return;
            }
        }

        Response.StatusCode = (int)HttpStatusCode.NotFound;
    }

    // Draft is the working copy and wins when requested; Published is the live page; Archived is the last
    // resort so that fully archived groups (ArchiveGroups sets every page to Archived) still render.
    private static IEnumerable<string> GetContentCandidates(GroupedPageBuilderPage group, bool draft)
    {
        if (draft)
        {
            foreach (var page in group.Pages.Where(x => x.Status == Draft))
            {
                yield return page.Id;
            }
        }

        foreach (var page in group.Pages.Where(x => x.Status == Published))
        {
            yield return page.Id;
        }

        foreach (var page in group.Pages.Where(x => x.Status == Archived).OrderByDescending(x => x.ModifiedDate))
        {
            yield return page.Id;
        }
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

        var content = await ReadRequestContentAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content) || !IsWellFormedJson(content))
        {
            return BadRequest("Page content must be a non-empty, well-formed JSON document.");
        }

        var validationError = await ValidateSharedComponentContentAsync(
            groupedPage.StoreId,
            content,
            cancellationToken);
        if (validationError != null)
        {
            return validationError;
        }

        var writeResult = await pageContentService.SaveContentAsync(
            groupId,
            groupedPage,
            content,
            cancellationToken);
        if (writeResult.ErrorMessage != null)
        {
            return BadRequest(writeResult.ErrorMessage);
        }

        return NoContent();
    }

    [HttpPost("grouped/{targetGroupId}/content/{sourceGroupId}")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<IActionResult> CopyPageContent(
        [FromRoute] string targetGroupId,
        [FromRoute] string sourceGroupId,
        CancellationToken cancellationToken = default)
    {
        var sourceGroup = await groupedPageService.GetByIdAsync(sourceGroupId);
        var targetGroup = await groupedPageService.GetByIdAsync(targetGroupId);

        if (sourceGroup == null || targetGroup == null)
        {
            return NotFound();
        }

        var sourceAuth = await authorizationService.AuthorizeAsync(User, sourceGroup, new PageBuilderAuthorizationRequirement());
        var targetAuth = await authorizationService.AuthorizeAsync(User, targetGroup, new PageBuilderAuthorizationRequirement());
        if (!sourceAuth.Succeeded || !targetAuth.Succeeded)
        {
            return Forbidden;
        }

        var sourcePageId = GetCopySourcePageId(sourceGroup);
        if (sourcePageId == null)
        {
            return NotFound();
        }

        var sourceContent = await groupedPageService.LoadContent(sourcePageId, cancellationToken);
        var validationError = await ValidateSharedComponentContentAsync(
            targetGroup.StoreId,
            sourceContent,
            cancellationToken);
        if (validationError != null)
        {
            return validationError;
        }

        var writeResult = await pageContentService.CopyContentAsync(
            targetGroupId,
            targetGroup,
            sourcePageId,
            cancellationToken);
        if (!writeResult.PageFound)
        {
            return NotFound();
        }

        if (writeResult.ErrorMessage != null)
        {
            return BadRequest(writeResult.ErrorMessage);
        }

        return NoContent();
    }

    private async Task<ActionResult> AuthorizeGroupUpdateAsync(
        GroupedPageBuilderPage groupedPage,
        GroupedPageBuilderPage model,
        bool storeChanged)
    {
        // Authorize the persisted resource first. Authorizing only the incoming model would let a caller spoof
        // a store they can access and then learn the existing group's store from validation details.
        var authorizationResult = await authorizationService.AuthorizeAsync(
            User,
            groupedPage ?? model,
            new PageBuilderAuthorizationRequirement());
        if (!authorizationResult.Succeeded)
        {
            return Forbidden;
        }

        if (!storeChanged)
        {
            return null;
        }

        // A move crosses two resource boundaries: the caller must be allowed to update both the persisted
        // source and the requested destination store.
        authorizationResult = await authorizationService.AuthorizeAsync(
            User,
            model,
            new PageBuilderAuthorizationRequirement());

        return authorizationResult.Succeeded ? null : Forbidden;
    }

    private async Task<ActionResult> ValidateSharedComponentContentAsync(
        string storeId,
        string content,
        CancellationToken cancellationToken)
    {
        bool hasSharedComponents;
        try
        {
            hasSharedComponents = PageBuilderPageContentService.HasSharedComponentReferences(content);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }

        if (hasSharedComponents)
        {
            var authorizationResult = await authorizationService.AuthorizeAsync(
                User,
                null,
                ModuleConstants.Security.Permissions.SharedComponentsRead);
            if (!authorizationResult.Succeeded)
            {
                return Forbidden;
            }
        }

        try
        {
            await pageContentService.ValidateReferencesForStoreAsync(
                storeId,
                content,
                cancellationToken);
            return null;
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<string> ReadRequestContentAsync(CancellationToken cancellationToken)
    {
        // Validate the complete request before a draft row is created. A blank or truncated write would
        // otherwise shadow the published page and could later replace it during publishing.
        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static bool HasStoreChanged(
        GroupedPageBuilderPage groupedPage,
        GroupedPageBuilderPage model)
    {
        return groupedPage != null &&
            !string.Equals(groupedPage.StoreId, model.StoreId, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyGroupChanges(
        GroupedPageBuilderPage groupedPage,
        GroupedPageBuilderPage model,
        bool storeChanged)
    {
        groupedPage.Name = model.Name;
        groupedPage.Permalink = model.Permalink;
        groupedPage.CultureName = model.CultureName;
        groupedPage.StoreId = model.StoreId;

        if (storeChanged)
        {
            foreach (var page in groupedPage.Pages)
            {
                page.StoreId = model.StoreId;
            }
        }

        groupedPage.Visibility = model.Visibility;
        groupedPage.UserGroups = model.UserGroups;
        groupedPage.StartDate = model.StartDate;
        groupedPage.EndDate = model.EndDate;
        groupedPage.OrganizationId = model.OrganizationId;
    }

    private static string GetPublishedContentSourceId(GroupedPageBuilderPage groupedPage)
    {
        return groupedPage.Pages.Any(x => x.Status == Draft)
            ? null
            : groupedPage.Pages.FirstOrDefault(x => x.Status == Published)?.Id;
    }

    private static string GetCopySourcePageId(GroupedPageBuilderPage sourceGroup)
    {
        return sourceGroup.Pages
            .Where(x => x.Status == Draft || x.Status == Published)
            .OrderByDescending(x => x.ModifiedDate)
            .Select(x => x.Id)
            .FirstOrDefault();
    }

    private static ActionResult Forbidden => new ObjectResult(new { })
    {
        StatusCode = (int)HttpStatusCode.Forbidden,
    };

    // Only well-formedness is checked, not the document's shape: content stored by older designer versions can
    // be an array rather than the current { settings, content } object, and both still load. What this rules
    // out is a payload that never parses at all — a truncated upload, which is indistinguishable from a real
    // save once written, and which every reader downstream chokes on (SyncGroupSettingsToContent parses it).
    private static bool IsWellFormedJson(string content)
    {
        try
        {
            JsonNode.Parse(content);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
