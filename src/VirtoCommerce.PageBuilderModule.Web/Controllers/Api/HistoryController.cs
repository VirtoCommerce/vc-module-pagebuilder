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
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.PageBuilderModule.Web.Extensions;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/pagebuilder/history")]
[Authorize]
[RepositoryOwnershipConflict]
public class HistoryController(
    IContentHistory contentHistory,
    IGroupedPageService groupedPageService,
    PageBuilderPageContentService pageContentService,
    IThemeDraftService themeDraftService,
    IAuthorizationService authorizationService,
    IValidator<PageBuilderStoreRequest> storeRequestValidator,
    IValidator<ContentHistoryRequest> requestValidator) : Controller
{
    // the part of a version id a restored version is named by
    private const int VersionLabelLength = 7;

    /// <summary>Get whether the store keeps history</summary>
    [HttpGet("status")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<ContentHistoryStatusResult>> GetStatus([FromQuery] string storeId, CancellationToken cancellationToken = default)
    {
        var request = new PageBuilderStoreRequest { StoreId = storeId };
        if ((await CheckStoreAccessAsync(request) ?? await ValidateAsync(storeRequestValidator, request, cancellationToken)) is { } refused)
        {
            return refused;
        }

        var result = AbstractTypeFactory<ContentHistoryStatusResult>.TryCreateInstance();
        result.Status = await contentHistory.GetStatusAsync(storeId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get versions that changed any of the items, newest first</summary>
    [HttpGet]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
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
    [Authorize(ModuleConstants.Security.Permissions.Read)]
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
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<ContentHistoryRestoreResult>> Restore([FromRoute] string versionId, [FromQuery] string storeId,
        [FromQuery(Name = "item")] string[] items, CancellationToken cancellationToken = default)
    {
        var request = new ContentHistoryRequest { StoreId = storeId, Items = items ?? [], VersionId = versionId };
        if (await CheckRequestAsync(request, cancellationToken) is { } refused)
        {
            return refused;
        }

        var historyItems = ParseItems(request);
        if (await CheckEditPermissionsAsync(historyItems) is { } forbidden)
        {
            return forbidden;
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

        try
        {
            var themeFiles = contents.Where(x => x.Key.Type == HistoryItemType.Theme).ToDictionary(x => x.Key.Key, x => x.Value);
            var page = contents.SingleOrDefault(x => x.Key.Type == HistoryItemType.Page);
            await themeDraftService.SaveDraftsAsync(storeId, themeFiles, token => RestorePageAsync(page.Key?.Key, page.Value, token), cancellationToken);
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

        var result = AbstractTypeFactory<ContentHistoryRestoreResult>.TryCreateInstance();
        result.SaveId = Request.GetSaveId() ?? Guid.NewGuid().ToString("N");

        var record = AbstractTypeFactory<ContentHistoryRecord>.TryCreateInstance().WithUser(User);
        record.Description = $"{ModuleConstants.GitHistory.RestoredSummary} {versionId[..VersionLabelLength]}";
        record.SaveId = result.SaveId;
        foreach (var (historyItem, content) in contents)
        {
            record.AddEntry(historyItem, content);
        }

        await contentHistory.RecordAsync(storeId, record, cancellationToken);

        return Ok(result);
    }

    private async Task<bool> RestorePageAsync(string groupId, string content, CancellationToken cancellationToken)
    {
        if (groupId == null)
        {
            return false;
        }

        var group = await groupedPageService.GetByIdAsync(groupId) ?? throw new KeyNotFoundException();
        var current = await pageContentService.LoadGroupContentAsync(group, draft: true, cancellationToken);
        await pageContentService.SaveConditionalContentAsync(group, content, current.ETag, cancellationToken);

        return true;
    }

    // store access first, so a 400 does not reveal which stores exist
    private async Task<ActionResult> CheckRequestAsync(ContentHistoryRequest request, CancellationToken cancellationToken)
    {
        return await CheckStoreAccessAsync(request)
               ?? await ValidateAsync(requestValidator, request, cancellationToken)
               ?? await CheckPageAccessAsync(request.StoreId, ParseItems(request));
    }

    private async Task<ActionResult> CheckStoreAccessAsync(PageBuilderStoreRequest request)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, request, new PageBuilderAuthorizationRequirement());
        return authorizationResult.Succeeded ? null : Forbid();
    }

    private async Task<ActionResult> CheckPageAccessAsync(string storeId, IEnumerable<HistoryItem> items)
    {
        foreach (var item in items.Where(x => x.Type == HistoryItemType.Page))
        {
            var group = await groupedPageService.GetByIdAsync(item.Key);
            if (group == null)
            {
                return NotFound();
            }

            var authorizationResult = await authorizationService.AuthorizeAsync(User, group, new PageBuilderAuthorizationRequirement());
            if (!authorizationResult.Succeeded || !group.StoreId.EqualsIgnoreCase(storeId))
            {
                return Forbid();
            }
        }

        return null;
    }

    private async Task<ActionResult> CheckEditPermissionsAsync(IEnumerable<HistoryItem> items)
    {
        var permissions = items
            .Select(x => x.Type == HistoryItemType.Page ? ModuleConstants.Security.Permissions.Update : PageBuilderContentPaths.GetThemeFileEditPermission(x.Key))
            .Distinct();
        foreach (var permission in permissions)
        {
            if (!(await authorizationService.AuthorizeAsync(User, null, permission)).Succeeded)
            {
                return Forbid();
            }
        }

        return null;
    }

    private static async Task<ActionResult> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        return validationResult.IsValid
            ? null
            : new BadRequestObjectResult(new { error = string.Join(" ", validationResult.Errors.Select(x => x.ErrorMessage)) });
    }

    private static IList<HistoryItem> ParseItems(ContentHistoryRequest request) =>
        request.Items.Select(x => HistoryItem.TryParse(x, out var item) ? item : null).Where(x => x != null).ToList();
}
