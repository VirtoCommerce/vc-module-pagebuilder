using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-shared-components")]
[Authorize]
public class PageBuilderSharedComponentsController(
    IPageBuilderSharedComponentService sharedComponentService,
    IPageBuilderSharedComponentSearchService sharedComponentSearchService,
    IPageBuilderSharedComponentContentService sharedComponentContentService,
    IPageBuilderSharedComponentUsageService sharedComponentUsageService,
    IAuthorizationService authorizationService)
    : Controller
{
    [HttpPost("search")]
    [Authorize(ModuleConstants.Security.Permissions.SharedComponentsRead)]
    public async Task<ActionResult<PageBuilderSharedComponentSearchResult>> Search(
        [FromBody] PageBuilderSharedComponentSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria == null || string.IsNullOrWhiteSpace(criteria.StoreId))
        {
            return BadRequest("StoreId is required.");
        }

        if (!await IsAuthorizedAsync(criteria))
        {
            return Forbidden;
        }

        var result = await sharedComponentSearchService.SearchAsync(criteria);
        await ApplyUsageAsync(result.Results, includePages: false, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(ModuleConstants.Security.Permissions.SharedComponentsRead)]
    public async Task<ActionResult<PageBuilderSharedComponent>> Get(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        var component = await sharedComponentService.GetByIdAsync(id);
        if (component == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(component))
        {
            return Forbidden;
        }

        await ApplyUsageAsync(
            [component],
            includePages: await CanReadPagesAsync(),
            cancellationToken);
        return Ok(component);
    }

    [HttpPost]
    [Authorize(ModuleConstants.Security.Permissions.SharedComponentsCreate)]
    [ProducesResponseType(typeof(PageBuilderSharedComponent), StatusCodes.Status201Created)]
    public async Task<ActionResult<PageBuilderSharedComponent>> Create(
        [FromBody] PageBuilderSharedComponentCreateModel request,
        CancellationToken cancellationToken = default)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.StoreId) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            request.Content == null)
        {
            return BadRequest("StoreId, Name, and Content are required.");
        }

        if (request.Name.Trim().Length > ModuleConstants.SharedComponents.NameMaxLength)
        {
            return BadRequest($"Name cannot exceed {ModuleConstants.SharedComponents.NameMaxLength} characters.");
        }

        var component = AbstractTypeFactory<PageBuilderSharedComponent>.TryCreateInstance();
        component.StoreId = request.StoreId;
        component.Name = request.Name;

        if (!await IsAuthorizedAsync(component))
        {
            return Forbidden;
        }

        try
        {
            await sharedComponentService.SaveWithContentAsync(
                component,
                request.Content.ToString(Formatting.None),
                cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }

        return CreatedAtAction(nameof(Get), new { id = component.Id }, component);
    }

    [HttpPut("{id}")]
    [Authorize(ModuleConstants.Security.Permissions.SharedComponentsUpdate)]
    public async Task<ActionResult<PageBuilderSharedComponent>> Update(
        [FromRoute] string id,
        [FromBody] PageBuilderSharedComponentUpdateModel request,
        CancellationToken cancellationToken = default)
    {
        var component = await sharedComponentService.GetByIdAsync(id);
        if (component == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(component))
        {
            return Forbidden;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        if (request.Name.Trim().Length > ModuleConstants.SharedComponents.NameMaxLength)
        {
            return BadRequest($"Name cannot exceed {ModuleConstants.SharedComponents.NameMaxLength} characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.StoreId) &&
            !string.Equals(request.StoreId, component.StoreId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("A Shared Component cannot be moved to another store.");
        }

        component.Name = request.Name;
        component = await sharedComponentService.UpdateMetadataAsync(component, cancellationToken);
        if (component == null)
        {
            // The component may have been deleted after the authorization read. The metadata service reloads
            // it under the component row lock and never turns that race into an upsert.
            return NotFound();
        }

        await ApplyUsageAsync(
            [component],
            includePages: await CanReadPagesAsync(),
            cancellationToken);

        return Ok(component);
    }

    [HttpDelete("{id}")]
    [Authorize(ModuleConstants.Security.Permissions.SharedComponentsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(PageBuilderSharedComponent), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        var component = await sharedComponentService.GetByIdAsync(id);
        if (component == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(component))
        {
            return Forbidden;
        }

        var includeUsagePages = await CanReadPagesAsync();
        await ApplyUsageAsync([component], includeUsagePages, cancellationToken);
        if (component.UsageCount > 0)
        {
            return Conflict(component);
        }

        try
        {
            if (!await sharedComponentService.TryDeleteAsync(component, cancellationToken))
            {
                return NotFound();
            }
        }
        catch (DbUpdateException)
        {
            await ApplyUsageAsync([component], includeUsagePages, cancellationToken);
            if (component.UsageCount > 0)
            {
                return Conflict(component);
            }

            throw;
        }

        return NoContent();
    }

    [HttpGet("{id}/content")]
    [Authorize(ModuleConstants.Security.Permissions.SharedComponentsRead)]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetContent(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        var component = await sharedComponentService.GetByIdAsync(id);
        if (component == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(component))
        {
            return Forbidden;
        }

        var content = await sharedComponentContentService.TryLoadContentAsync(component, cancellationToken);
        return string.IsNullOrWhiteSpace(content)
            ? NotFound()
            : Content(content, "application/json");
    }

    [HttpPut("{id}/content")]
    [HttpPost("{id}/content")]
    [Authorize(ModuleConstants.Security.Permissions.SharedComponentsUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SaveContent(
        [FromRoute] string id,
        [FromBody] JObject content,
        CancellationToken cancellationToken = default)
    {
        var component = await sharedComponentService.GetByIdAsync(id);
        if (component == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(component))
        {
            return Forbidden;
        }

        if (content == null)
        {
            return BadRequest("Content is required.");
        }

        try
        {
            if (!await sharedComponentContentService.TrySaveContentAsync(
                    component,
                    content.ToString(Formatting.None),
                    cancellationToken))
            {
                return NotFound();
            }
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }

        return NoContent();
    }

    private async Task ApplyUsageAsync(
        IList<PageBuilderSharedComponent> components,
        bool includePages,
        CancellationToken cancellationToken)
    {
        foreach (var storeGroup in components.GroupBy(x => x.StoreId, StringComparer.OrdinalIgnoreCase))
        {
            var usages = await sharedComponentUsageService.GetUsageAsync(
                storeGroup.Select(x => x.Id),
                storeGroup.Key,
                includePages,
                cancellationToken);
            var usagesById = usages.ToDictionary(x => x.SharedComponentId, StringComparer.OrdinalIgnoreCase);

            foreach (var component in storeGroup)
            {
                if (usagesById.TryGetValue(component.Id, out var usage))
                {
                    component.UsageCount = usage.UsageCount;
                    component.UsagePages = includePages ? usage.Pages : [];
                }
            }
        }
    }

    private async Task<bool> IsAuthorizedAsync(object resource)
    {
        var result = await authorizationService.AuthorizeAsync(
            User,
            resource,
            new PageBuilderAuthorizationRequirement());
        return result.Succeeded;
    }

    private async Task<bool> CanReadPagesAsync()
    {
        var result = await authorizationService.AuthorizeAsync(
            User,
            null,
            ModuleConstants.Security.Permissions.Read);
        return result.Succeeded;
    }

    private static ActionResult Forbidden => new ObjectResult(new { })
    {
        StatusCode = (int)HttpStatusCode.Forbidden,
    };
}
