using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

[Route("api/page-builder-multilanguage-pages")]
[Authorize]
public class MultilanguagePageController(
    IMultilanguagePageService multilanguagePageService,
    IMultilanguagePageSearchService multilanguagePageSearchService,
    IGroupedPageService groupedPageService,
    IAuthorizationService authorizationService,
    IStoreService storeService)
    : Controller
{
    [HttpPost("search")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<MultilanguagePageSearchResult>> Search([FromBody] MultilanguagePageSearchCriteria criteria)
    {
        if (!await IsAuthorizedAsync(criteria))
        {
            return Forbidden;
        }

        return Ok(await multilanguagePageSearchService.SearchAsync(criteria));
    }

    [HttpGet("{id}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<MultilanguagePage>> Get([FromRoute] string id)
    {
        var page = await multilanguagePageService.GetNoCloneAsync(id);
        if (page == null)
        {
            return NotFound();
        }

        return await IsAuthorizedAsync(page) ? Ok(page) : Forbidden;
    }

    [HttpPost]
    [Authorize(ModuleConstants.Security.Permissions.Create)]
    public async Task<ActionResult<MultilanguagePage>> Create([FromBody] MultilanguagePage model)
    {
        if (model == null ||
            string.IsNullOrWhiteSpace(model.StoreId) ||
            string.IsNullOrWhiteSpace(model.Name) ||
            string.IsNullOrWhiteSpace(model.Permalink))
        {
            return BadRequest("StoreId, Name and Permalink are required.");
        }

        if (!await IsAuthorizedAsync(model))
        {
            return Forbidden;
        }

        if (await storeService.GetNoCloneAsync(model.StoreId) == null)
        {
            return BadRequest($"Store '{model.StoreId}' does not exist.");
        }

        model.Id = null;
        await multilanguagePageService.SaveChangesAsync([model]);

        return Ok(model);
    }

    /// <summary>
    /// Updates the root; the shared name and inherited settings are propagated to the language versions.
    /// </summary>
    [HttpPut]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<MultilanguagePage>> Update([FromBody] MultilanguagePage model)
    {
        if (model == null ||
            string.IsNullOrWhiteSpace(model.Name) ||
            string.IsNullOrWhiteSpace(model.Permalink))
        {
            return BadRequest("Name and Permalink are required.");
        }

        var existing = await multilanguagePageService.GetByIdAsync(model.Id);
        if (existing == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(existing))
        {
            return Forbidden;
        }

        // A multilanguage page never moves to another store.
        model.StoreId = existing.StoreId;
        await multilanguagePageService.SaveChangesAsync([model]);

        return Ok(model);
    }

    /// <summary>
    /// Deletes the root. Its language versions are kept as Landing Pages.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(ModuleConstants.Security.Permissions.Delete)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete([FromRoute] string id)
    {
        var page = await multilanguagePageService.GetNoCloneAsync(id);
        if (page == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(page))
        {
            return Forbidden;
        }

        await multilanguagePageService.DeleteAsync([id]);

        return NoContent();
    }

    [HttpGet("{id}/languages")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public Task<ActionResult<IList<MultilanguagePageLanguage>>> GetLanguages([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        return ExecuteForRootAsync(id, () => multilanguagePageService.GetLanguagesAsync(id, cancellationToken));
    }

    /// <summary>
    /// Creates a draft version for a missing store language.
    /// </summary>
    /// <param name="id">Multilanguage page id</param>
    /// <param name="cultureName">Store language to create</param>
    /// <param name="copyContent">Start from the content of the default language version</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{id}/languages/{cultureName}")]
    [Authorize(ModuleConstants.Security.Permissions.Create)]
    public Task<ActionResult<GroupedPageBuilderPage>> CreateLanguage(
        [FromRoute] string id,
        [FromRoute] string cultureName,
        [FromQuery] bool copyContent = true,
        CancellationToken cancellationToken = default)
    {
        return ExecuteForRootAsync(id, () => multilanguagePageService.CreateLanguageAsync(id, cultureName, copyContent, cancellationToken));
    }

    /// <summary>
    /// Makes an existing page a language version of the multilanguage page.
    /// </summary>
    [HttpPost("{id}/link/{groupId}")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<GroupedPageBuilderPage>> Link([FromRoute] string id, [FromRoute] string groupId, CancellationToken cancellationToken = default)
    {
        var group = await groupedPageService.GetNoCloneAsync(groupId);
        if (group == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(group))
        {
            return Forbidden;
        }

        return await ExecuteForRootAsync(id, () => multilanguagePageService.LinkAsync(id, groupId, cancellationToken));
    }

    /// <summary>
    /// Turns a Landing Page into a Multilanguage Page with the page as its default language version.
    /// </summary>
    [HttpPost("convert/{groupId}")]
    [Authorize(ModuleConstants.Security.Permissions.Create)]
    public async Task<ActionResult<MultilanguagePage>> Convert([FromRoute] string groupId, CancellationToken cancellationToken = default)
    {
        var group = await groupedPageService.GetNoCloneAsync(groupId);
        if (group == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(group))
        {
            return Forbidden;
        }

        try
        {
            return Ok(await multilanguagePageService.ConvertAsync(groupId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Marks an outdated translation as matching the current source language publication.
    /// </summary>
    [HttpPost("mark-up-to-date/{groupId}")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<GroupedPageBuilderPage>> MarkUpToDate([FromRoute] string groupId, CancellationToken cancellationToken = default)
    {
        var group = await groupedPageService.GetNoCloneAsync(groupId);
        if (group == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(group))
        {
            return Forbidden;
        }

        if (string.IsNullOrEmpty(group.MultilanguagePageId))
        {
            return BadRequest($"Page '{groupId}' is not a language version.");
        }

        return Ok(await multilanguagePageService.MarkUpToDateAsync(groupId, cancellationToken));
    }

    /// <summary>
    /// Turns a language version back into a Landing Page.
    /// </summary>
    [HttpPost("unlink/{groupId}")]
    [Authorize(ModuleConstants.Security.Permissions.Update)]
    public async Task<ActionResult<GroupedPageBuilderPage>> Unlink([FromRoute] string groupId, CancellationToken cancellationToken = default)
    {
        var group = await groupedPageService.GetNoCloneAsync(groupId);
        if (group == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(group))
        {
            return Forbidden;
        }

        return Ok(await multilanguagePageService.UnlinkAsync(groupId, cancellationToken));
    }

    [HttpPost("{id}/publish-all")]
    [Authorize(ModuleConstants.Security.Permissions.Publish)]
    public Task<ActionResult<MultilanguagePageBatchResult>> PublishAll([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        return ExecuteForRootAsync(id, () => multilanguagePageService.PublishAllAsync(id, cancellationToken));
    }

    [HttpPost("{id}/archive-all")]
    [Authorize(ModuleConstants.Security.Permissions.Delete)]
    public Task<ActionResult<MultilanguagePageBatchResult>> ArchiveAll([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        return ExecuteForRootAsync(id, () => multilanguagePageService.ArchiveAllAsync(id, cancellationToken));
    }

    private async Task<ActionResult<T>> ExecuteForRootAsync<T>(string id, Func<Task<T>> action)
    {
        var page = await multilanguagePageService.GetNoCloneAsync(id);
        if (page == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedAsync(page))
        {
            return Forbidden;
        }

        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<bool> IsAuthorizedAsync(object resource)
    {
        var result = await authorizationService.AuthorizeAsync(User, resource, new PageBuilderAuthorizationRequirement());
        return result.Succeeded;
    }

    private static ActionResult Forbidden => new ObjectResult(new { })
    {
        StatusCode = (int)HttpStatusCode.Forbidden,
    };
}
