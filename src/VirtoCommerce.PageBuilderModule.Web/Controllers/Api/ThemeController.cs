using System;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Web.Extensions;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/pagebuilder")]
[Authorize]
public class ThemeController(
    IThemePublisher themePublisher,
    ISchemaCatalogService schemaCatalogService,
    IThemePaletteService themePaletteService,
    IAuthorizationService authorizationService,
    IValidator<PageBuilderStoreRequest> storeRequestValidator,
    IValidator<ThemePaletteGenerationRequest> paletteGenerationRequestValidator,
    IValidator<ThemePaletteCheckRequest> paletteCheckRequestValidator) : Controller
{
    /// <summary>Publish all theme drafts of the store</summary>
    [HttpPost("theme/publish")]
    [Authorize(ModuleConstants.Security.Permissions.Publish)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ThemePublicationPointer>> Publish([FromQuery] string storeId, [FromQuery] string catalogHash, CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, cancellationToken, catalogHash) is { } refused)
        {
            return refused;
        }

        try
        {
            var result = AbstractTypeFactory<ThemePublicationPointer>.TryCreateInstance();
            result.Publication = await themePublisher.PublishAsync(storeId, catalogHash, User, cancellationToken);
            return Ok(result);
        }
        catch (ThemeValidationException ex)
        {
            return BadRequest(new { error = ex.Message, errors = ex.Errors });
        }
    }

    /// <summary>Get the current theme publication and the theme files with a draft</summary>
    [HttpGet("theme/status")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ThemeStatus>> GetStatus([FromQuery] string storeId, CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        return Ok(await themePublisher.GetStatusAsync(storeId, cancellationToken));
    }

    /// <summary>Save storefront schema catalog</summary>
    [HttpPut("catalog")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SchemaCatalogPointer>> SaveCatalog([FromQuery] string storeId, CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        // read as text: the service parses the catalog itself, while MVC's JSON settings would turn its dates to UTC
        using var reader = new StreamReader(Request.Body);
        var catalog = await reader.ReadToEndAsync(cancellationToken);
        try
        {
            var result = AbstractTypeFactory<SchemaCatalogPointer>.TryCreateInstance();
            result.Hash = await schemaCatalogService.SaveAsync(storeId, catalog, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get schema catalog by hash, or the current one</summary>
    [HttpGet("catalog")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetCatalog([FromQuery] string storeId, [FromQuery] string hash, CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, cancellationToken, hash) is { } refused)
        {
            return refused;
        }

        var catalog = await schemaCatalogService.GetAsync(storeId, hash, cancellationToken);
        return catalog == null ? NotFound() : Content(catalog, MediaTypeNames.Application.Json);
    }

    /// <summary>Generate light and dark palettes that meet WCAG AA contrast from one or two brand colors</summary>
    [HttpPost("theme/palette")]
    [Authorize(ModuleConstants.Security.Permissions.Theme)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ThemePaletteGenerationResult>> GeneratePalette([FromQuery] string storeId, [FromBody] ThemePaletteGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if ((await CheckRequestAsync(storeId, cancellationToken) ?? await ValidateBodyAsync(paletteGenerationRequestValidator, request, cancellationToken)) is { } refused)
        {
            return refused;
        }

        return Ok(themePaletteService.GeneratePalette(request));
    }

    /// <summary>Get the contrast pairs a palette fails</summary>
    [HttpPost("theme/palette/check")]
    [Authorize(ModuleConstants.Security.Permissions.Theme)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ThemePaletteCheckResult>> CheckPalette([FromQuery] string storeId, [FromBody] ThemePaletteCheckRequest request, CancellationToken cancellationToken = default)
    {
        if ((await CheckRequestAsync(storeId, cancellationToken) ?? await ValidateBodyAsync(paletteCheckRequestValidator, request, cancellationToken)) is { } refused)
        {
            return refused;
        }

        return Ok(themePaletteService.CheckPalette(request));
    }

    // access first, so a 400 does not reveal which stores exist
    private async Task<ActionResult> CheckRequestAsync(string storeId, CancellationToken cancellationToken, string catalogHash = null)
    {
        var request = new PageBuilderStoreRequest { StoreId = storeId, CatalogHash = catalogHash };

        return await CheckStoreAccessAsync(request) ?? await storeRequestValidator.ValidateRequestAsync(request, ModelState, cancellationToken);
    }

    private async Task<ActionResult> CheckStoreAccessAsync(PageBuilderStoreRequest request)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, request, new PageBuilderAuthorizationRequirement());
        return authorizationResult.Succeeded ? null : Forbid();
    }

    private async Task<ActionResult> ValidateBodyAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new BadRequestObjectResult(new { error = "The request has no body." });
        }

        return await validator.ValidateRequestAsync(request, ModelState, cancellationToken);
    }
}
