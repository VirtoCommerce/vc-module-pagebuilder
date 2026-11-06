using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Web.Extensions;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-pages/grouped/{groupId}/history")]
[Authorize]
[RepositoryOwnershipConflict]
public class GroupedPageHistoryController(
    IGroupedPageService groupedPageService,
    IGroupedPageContentService groupedPageContentService,
    PageBuilderPageContentService pageContentService,
    IAuthorizationService authorizationService,
    IGroupedPageHistoryService groupedPageHistoryService) : Controller
{
    private const string NeverPublishedError = "This page has never been published, so there is no published version to go back to.";

    /// <summary>Get changes of the page, newest first</summary>
    [HttpGet]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GitFileHistoryResult>> GetHistory([FromRoute] string groupId, [FromQuery] string after = null,
        [FromQuery] int take = ModuleConstants.GitHistory.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var group = await GetAuthorizedGroupAsync(groupId);
        if (group == null)
        {
            return NotFound();
        }

        try
        {
            var history = await groupedPageHistoryService.GetHistoryAsync(group, after, take, cancellationToken);
            return history == null ? NotFound() : Ok(history);
        }
        catch (GitContentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get page content at the change</summary>
    [HttpGet("{sha}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult> GetVersion([FromRoute] string groupId, [FromRoute] string sha, CancellationToken cancellationToken = default)
    {
        var group = await GetAuthorizedGroupAsync(groupId);
        if (group == null || !GitCommitSha.IsSha(sha))
        {
            return NotFound();
        }

        var content = await groupedPageHistoryService.ReadContentAsync(group, sha, cancellationToken);
        return content == null ? NotFound() : Content(content, MediaTypeNames.Application.Json);
    }

    /// <summary>Restore page draft from the change</summary>
    [HttpPost("{sha}/restore")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<UpdatePageContentResult>> Restore([FromRoute] string groupId, [FromRoute] string sha, [FromQuery] string version = null,
        CancellationToken cancellationToken = default)
    {
        var group = await GetAuthorizedGroupAsync(groupId);
        if (group == null || !GitCommitSha.IsSha(sha))
        {
            return NotFound();
        }

        var content = await groupedPageHistoryService.ReadContentAsync(group, sha, cancellationToken);
        if (content == null)
        {
            return NotFound();
        }

        var current = await groupedPageContentService.LoadGroupContentAsync(group, draft: true, cancellationToken);
        PageBuilderConditionalContentWriteResult writeResult;
        try
        {
            writeResult = await pageContentService.SaveConditionalContentAsync(group, content, current.ETag, cancellationToken);
        }
        catch (PageBuilderContentConflictException ex)
        {
            return StatusCode(StatusCodes.Status412PreconditionFailed, ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }

        await groupedPageHistoryService.RecordRestoreAsync(group, content, sha, version, User, Request.GetSaveId(), cancellationToken);

        return Ok(new UpdatePageContentResult { ETag = writeResult.ETag });
    }

    /// <summary>Discard unpublished changes of the page</summary>
    [HttpPost("discard")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult> Discard([FromRoute] string groupId, CancellationToken cancellationToken = default)
    {
        var group = await GetAuthorizedGroupAsync(groupId);
        if (group == null)
        {
            return NotFound();
        }

        if (!await groupedPageHistoryService.DiscardDraftAsync(group, User, Request.GetSaveId(), cancellationToken))
        {
            return BadRequest(new { error = NeverPublishedError });
        }

        return Ok();
    }

    private async Task<GroupedPageBuilderPage> GetAuthorizedGroupAsync(string groupId)
    {
        var group = await groupedPageService.GetByIdAsync(groupId);
        if (group == null)
        {
            return null;
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, group, new PageBuilderAuthorizationRequirement());
        return authorizationResult.Succeeded ? group : null;
    }
}
