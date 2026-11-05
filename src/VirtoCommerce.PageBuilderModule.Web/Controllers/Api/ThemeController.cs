using System;
using System.IO;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

/// <summary>A store's theme as one publication, and the storefront schema catalogs it is validated against.</summary>
[Route("api/pagebuilder")]
[Authorize]
public class ThemeController(
    IThemePublisher themePublisher,
    ISchemaCatalogService schemaCatalogService,
    IAuthorizationService authorizationService,
    IValidator<PageBuilderStoreRequest> storeRequestValidator) : Controller
{
    private const string JsonContentType = "application/json";

    /// <summary>Puts every theme draft of the store live as one publication, validated against the catalog the editor previewed.</summary>
    [HttpPost("theme/publish")]
    [Authorize(ModuleConstants.Security.Permissions.Publish)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Publish([FromQuery] string storeId, [FromQuery] string catalogHash)
    {
        if (await CheckRequestAsync(storeId) is { } refused)
        {
            return refused;
        }

        try
        {
            var publication = await themePublisher.PublishAsync(storeId, catalogHash, User, HttpContext.RequestAborted);
            return Ok(new { publication });
        }
        catch (ThemeValidationException failure)
        {
            return BadRequest(new { error = failure.Message, errors = failure.Errors });
        }
    }

    /// <summary>Keeps the catalog of the storefront the designer previews, under the hash it carries.</summary>
    [HttpPut("catalog")]
    [Authorize(ModuleConstants.Security.Permissions.Theme)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> SaveCatalog([FromQuery] string storeId)
    {
        if (await CheckRequestAsync(storeId) is { } refused)
        {
            return refused;
        }

        using var reader = new StreamReader(Request.Body);
        var catalog = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        try
        {
            var hash = await schemaCatalogService.SaveAsync(storeId, catalog, HttpContext.RequestAborted);
            return Ok(new { hash });
        }
        catch (ArgumentException failure)
        {
            return BadRequest(new { error = failure.Message });
        }
    }

    /// <summary>The catalog with that hash, the store's current one without it.</summary>
    [HttpGet("catalog")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetCatalog([FromQuery] string storeId, [FromQuery] string hash)
    {
        if (await CheckRequestAsync(storeId) is { } refused)
        {
            return refused;
        }

        var catalog = await schemaCatalogService.GetAsync(storeId, hash, HttpContext.RequestAborted);
        return catalog == null ? NotFound() : Content(catalog, JsonContentType);
    }

    private Task<ActionResult> CheckRequestAsync(string storeId) =>
        this.CheckStoreRequestAsync(authorizationService, storeRequestValidator, new PageBuilderStoreRequest { StoreId = storeId });
}
