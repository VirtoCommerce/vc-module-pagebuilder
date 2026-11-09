using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Web.Extensions;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/pagebuilder/history")]
[Authorize]
[RepositoryOwnershipConflict]
public class HistoryController(
    IContentHistory contentHistory,
    IGroupedPageService groupedPageService,
    IDesignerContentService designerContentService,
    IContentPermissionService contentPermissionService,
    IPageBuilderSharedComponentPlacementValidator sharedComponentPlacementValidator,
    IAuthorizationService authorizationService,
    IValidator<PageBuilderStoreRequest> storeRequestValidator,
    IValidator<ContentHistoryRequest> requestValidator,
    IValidator<ContentHistoryRestoreRequest> restoreRequestValidator) : Controller
{
    /// <summary>Get whether the store keeps history</summary>
    [HttpGet("status")]
    public async Task<ActionResult<ContentHistoryStatusResult>> GetStatus([FromQuery] string storeId, CancellationToken cancellationToken = default)
    {
        var request = new PageBuilderStoreRequest { StoreId = storeId };
        if ((await CheckStoreAccessAsync(request) ?? await storeRequestValidator.ValidateRequestAsync(request, cancellationToken)) is { } refused)
        {
            return refused;
        }

        var result = AbstractTypeFactory<ContentHistoryStatusResult>.TryCreateInstance();
        result.Status = await contentHistory.GetStatusAsync(storeId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get versions that changed any of the items, newest first</summary>
    [HttpGet]
    public async Task<ActionResult<ContentVersionPage>> GetHistory([FromQuery] string storeId, [FromQuery(Name = "item")] string[] items,
        [FromQuery] string cursor = null, [FromQuery] int take = ModuleConstants.GitHistory.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var request = new ContentHistoryRequest { StoreId = storeId, Items = items ?? [], Cursor = cursor, Take = take };
        if (await CheckRequestAsync(request, cancellationToken) is { } refused)
        {
            return refused;
        }

        try
        {
            return Ok(await contentHistory.ListAsync(storeId, ParseItems(request), cursor, take, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (GitContentException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>Get the item as of the version</summary>
    [HttpGet("{versionId}")]
    public async Task<ActionResult> GetVersion([FromRoute] string versionId, [FromQuery] string storeId, [FromQuery] string item,
        CancellationToken cancellationToken = default)
    {
        var request = new ContentHistoryRequest { StoreId = storeId, Items = item == null ? [] : [item], VersionId = versionId };
        if (await CheckRequestAsync(request, cancellationToken) is { } refused)
        {
            return refused;
        }

        try
        {
            var content = await contentHistory.ReadAsync(storeId, ParseItems(request).Single(), versionId, cancellationToken);
            return content == null ? NotFound() : Content(content, MediaTypeNames.Application.Json);
        }
        catch (GitContentException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>Restore the items as of the version as their drafts, in one save</summary>
    [HttpPost("{versionId}/restore")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<ContentSaveResult>> Restore([FromRoute] string versionId, [FromQuery] string storeId,
        [FromQuery(Name = "item")] string[] items, CancellationToken cancellationToken = default)
    {
        var request = new ContentHistoryRestoreRequest { StoreId = storeId, Items = items ?? [], VersionId = versionId };
        if ((await CheckStoreAccessAsync(request) ?? await restoreRequestValidator.ValidateRequestAsync(request, cancellationToken)) is { } refused)
        {
            return refused;
        }

        // the edit permissions first, so a 404 does not reveal which pages exist
        var historyItems = ParseItems(request);
        if (!await contentPermissionService.HasPermissionsAsync(User, historyItems.Select(contentPermissionService.GetEditPermission)))
        {
            return Forbid();
        }

        var pageItem = historyItems.SingleOrDefault(x => x.Type == HistoryItemType.Page);
        var group = pageItem == null ? null : await groupedPageService.GetByIdAsync(pageItem.Key);
        if (pageItem != null && await CheckPageAccessAsync(storeId, group) is { } refusedPage)
        {
            return refusedPage;
        }

        var contents = new Dictionary<HistoryItem, string>();
        try
        {
            foreach (var historyItem in historyItems)
            {
                var content = await contentHistory.ReadAsync(storeId, historyItem, versionId, cancellationToken);
                if (content == null)
                {
                    return NotFound();
                }

                contents[historyItem] = content;
            }
        }
        catch (GitContentException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }

        if (pageItem != null &&
            (await sharedComponentPlacementValidator.ValidateAsync(User, storeId, contents[pageItem], cancellationToken)).ToActionResult() is { } refusedContent)
        {
            return refusedContent;
        }

        var command = AbstractTypeFactory<ContentRestoreCommand>.TryCreateInstance();
        command.StoreId = storeId;
        command.VersionId = versionId;
        command.SaveId = Request.GetOrCreateSaveId();
        command.Group = group;
        command.Contents = contents;
        try
        {
            return Ok(await designerContentService.RestoreAsync(command, User, cancellationToken));
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
            return BadRequest(new { error = ex.Message });
        }
    }

    // store access first, so a 400 does not reveal which stores exist
    private async Task<ActionResult> CheckRequestAsync(ContentHistoryRequest request, CancellationToken cancellationToken)
    {
        return await CheckStoreAccessAsync(request)
               ?? await requestValidator.ValidateRequestAsync(request, cancellationToken)
               ?? await CheckPagesAccessAsync(request.StoreId, ParseItems(request));
    }

    private async Task<ActionResult> CheckStoreAccessAsync(PageBuilderStoreRequest request)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, request, new PageBuilderAuthorizationRequirement());
        return authorizationResult.Succeeded ? null : Forbid();
    }

    private async Task<ActionResult> CheckPagesAccessAsync(string storeId, IEnumerable<HistoryItem> items)
    {
        foreach (var item in items.Where(x => x.Type == HistoryItemType.Page))
        {
            if (await CheckPageAccessAsync(storeId, await groupedPageService.GetByIdAsync(item.Key)) is { } refused)
            {
                return refused;
            }
        }

        return null;
    }

    private async Task<ActionResult> CheckPageAccessAsync(string storeId, GroupedPageBuilderPage group)
    {
        if (group == null)
        {
            return NotFound();
        }

        return await contentPermissionService.CanAccessPageAsync(User, group, storeId) ? null : Forbid();
    }

    private static IList<HistoryItem> ParseItems(ContentHistoryRequest request) =>
        request.Items.Select(x => HistoryItem.TryParse(x, out var item) ? item : null).Where(x => x != null).ToList();
}
