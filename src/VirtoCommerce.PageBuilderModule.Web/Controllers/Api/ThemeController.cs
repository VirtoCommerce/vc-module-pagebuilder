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
using VirtoCommerce.PageBuilderModule.Web.Extensions;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/pagebuilder")]
[Authorize]
public class ThemeController(
    IThemePublisher themePublisher,
    ISchemaCatalogService schemaCatalogService,
    IThemePaletteService themePaletteService,
    IThemeSnapshotService themeSnapshotService,
    IAuthorizationService authorizationService,
    IValidator<PageBuilderStoreRequest> storeRequestValidator,
    IValidator<ThemePaletteGenerationRequest> paletteGenerationRequestValidator,
    IValidator<ThemePaletteCheckRequest> paletteCheckRequestValidator) : Controller
{
    [HttpPost("theme/publish")]
    [Authorize(ModuleConstants.Security.Permissions.Publish)]
    [ProducesResponseType(typeof(ThemePublicationPointer), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ThemePublicationPointer>> Publish([FromQuery] string storeId, [FromQuery] string catalogHash, CancellationToken cancellationToken = default)
    {
        if (await CheckRequestAsync(storeId, cancellationToken, catalogHash) is { } refused)
        {
            return refused;
        }

        if (!await themeSnapshotService.IsNewPageBuilderEnabledAsync(storeId, cancellationToken))
        {
            return BadRequest(new { error = "The store does not use the new Page Builder: its theme is not published with theme/publish." });
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

    [HttpGet("theme/status")]
    [ProducesResponseType(typeof(ThemeStatus), StatusCodes.Status200OK)]
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

    [HttpPut("catalog")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    [ProducesResponseType(typeof(SchemaCatalogPointer), StatusCodes.Status200OK)]
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

    [HttpGet("catalog")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    [HttpPost("theme/palette")]
    [Authorize(ModuleConstants.Security.Permissions.Theme)]
    [ProducesResponseType(typeof(ThemePaletteGenerationResult), StatusCodes.Status200OK)]
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

    [HttpPost("theme/palette/check")]
    [Authorize(ModuleConstants.Security.Permissions.Theme)]
    [ProducesResponseType(typeof(ThemePaletteCheckResult), StatusCodes.Status200OK)]
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

    private Task<ActionResult> CheckRequestAsync(string storeId, CancellationToken cancellationToken, string catalogHash = null)
    {
        var request = new PageBuilderStoreRequest { StoreId = storeId, CatalogHash = catalogHash };

        return authorizationService.CheckStoreRequestAsync(User, storeRequestValidator, request, ModelState, cancellationToken);
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
