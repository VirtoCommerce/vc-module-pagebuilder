using System;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.PageBuilderModule.Web.Extensions;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/pagebuilder/theme-history")]
[Authorize]
[RepositoryOwnershipConflict]
public class ThemeFileHistoryController(
    IThemeFileHistoryService themeFileHistoryService,
    IAuthorizationService authorizationService,
    IValidator<ThemeFileRequest> requestValidator) : Controller
{
    /// <summary>Get changes of the theme file, newest first</summary>
    [HttpGet]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<GitFileHistoryResult>> GetHistory([FromQuery] string storeId, [FromQuery] string path, [FromQuery] string after = null,
        [FromQuery] int take = ModuleConstants.GitHistory.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, path, cancellationToken) is { } refused)
        {
            return refused;
        }

        try
        {
            var history = await themeFileHistoryService.GetHistoryAsync(storeId, path, after, take, cancellationToken);
            return history == null ? NotFound() : Ok(history);
        }
        catch (GitContentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get theme file content at the change</summary>
    [HttpGet("{sha}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult> GetVersion([FromRoute] string sha, [FromQuery] string storeId, [FromQuery] string path, CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, path, cancellationToken) is { } refused)
        {
            return refused;
        }

        var content = GitCommitSha.IsSha(sha) ? await themeFileHistoryService.ReadContentAsync(storeId, path, sha, cancellationToken) : null;
        return content == null ? NotFound() : Content(content, MediaTypeNames.Application.Json);
    }

    /// <summary>Restore theme file draft from the change</summary>
    [HttpPost("{sha}/restore")]
    public async Task<ActionResult> Restore([FromRoute] string sha, [FromQuery] string storeId, [FromQuery] string path, [FromQuery] string version = null,
        CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, path, cancellationToken) is { } refused)
        {
            return refused;
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, null, GetEditPermission(path));
        if (!authorizationResult.Succeeded)
        {
            return Forbid();
        }

        if (!GitCommitSha.IsSha(sha) || !await themeFileHistoryService.RestoreAsync(storeId, path, sha, version, User, Request.GetSaveId(), cancellationToken))
        {
            return NotFound();
        }

        return Ok();
    }

    // access first, so a 400 does not reveal which stores exist
    private async Task<ActionResult> CheckRequestAsync(string storeId, string path, CancellationToken cancellationToken)
    {
        var request = new ThemeFileRequest { StoreId = storeId, Path = path };

        return await CheckStoreAccessAsync(request) ?? await ValidateAsync(request, cancellationToken);
    }

    private async Task<ActionResult> CheckStoreAccessAsync(ThemeFileRequest request)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, request, new PageBuilderAuthorizationRequirement());
        return authorizationResult.Succeeded ? null : Forbid();
    }

    private async Task<ActionResult> ValidateAsync(ThemeFileRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await requestValidator.ValidateAsync(request, cancellationToken);
        return validationResult.IsValid
            ? null
            : BadRequest(new { error = string.Join(" ", validationResult.Errors.Select(x => x.ErrorMessage)) });
    }

    private static string GetEditPermission(string path) =>
        PageBuilderContentPaths.IsTemplateFile(path) ? ModuleConstants.Security.Permissions.Templates : ModuleConstants.Security.Permissions.Theme;
}
