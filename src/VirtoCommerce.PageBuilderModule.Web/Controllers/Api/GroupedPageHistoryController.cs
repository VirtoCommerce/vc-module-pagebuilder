using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    /// <summary>
    /// A database page's version history (<see cref="GroupedPageHistory"/>): its changes, one change's content,
    /// going back to a change, and throwing the unpublished changes away. 404 for a store without history.
    /// </summary>
    [Route("api/page-builder-pages/grouped/{groupId}/history")]
    [Authorize]
    public class GroupedPageHistoryController(
        IGroupedPageService groupedPageService,
        IGroupedPageContentService groupedPageContentService,
        IPageBuilderPageService crudService,
        PageBuilderPageContentService pageContentService,
        IAuthorizationService authorizationService,
        GroupedPageHistory pageHistory) : Controller
    {
        [HttpGet("")]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
        public async Task<ActionResult<GroupedPageHistoryResult>> GetHistory([FromRoute] string groupId, [FromQuery] string after = null,
            [FromQuery] int take = 20, CancellationToken cancellationToken = default)
        {
            var group = await AuthorizedGroupAsync(groupId);
            if (group == null)
            {
                return NotFound();
            }

            try
            {
                var history = await pageHistory.GetAsync(group, after, take, cancellationToken);
                return history == null ? NotFound() : Ok(history);
            }
            catch (Exception failure) when (failure is HttpRequestException or InvalidOperationException)
            {
                return BadRequest(new { error = failure.Message });
            }
        }

        /// <summary>The page's content at a change, to preview it.</summary>
        [HttpGet("{sha}")]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
        public async Task<ActionResult> GetVersion([FromRoute] string groupId, [FromRoute] string sha, CancellationToken cancellationToken = default)
        {
            var group = await AuthorizedGroupAsync(groupId);
            if (group == null || !GitCommitSha.IsSha(sha))
            {
                return NotFound();
            }

            var content = await pageHistory.ReadAsync(group, sha, cancellationToken);
            return content == null ? NotFound() : Content(content, "application/json");
        }

        /// <summary>
        /// Makes a change's content the page's draft again — a new change on top, so nothing after it is lost;
        /// publishing it is a separate step, as after any save.
        /// </summary>
        [HttpPost("{sha}/restore")]
        [Authorize(ModuleConstants.Security.Permissions.Update)]
        public async Task<ActionResult> Restore([FromRoute] string groupId, [FromRoute] string sha, CancellationToken cancellationToken = default)
        {
            var group = await AuthorizedGroupAsync(groupId);
            if (group == null || !GitCommitSha.IsSha(sha))
            {
                return NotFound();
            }

            var content = await pageHistory.ReadAsync(group, sha, cancellationToken);
            if (content == null)
            {
                return NotFound();
            }

            // the editor asked for exactly this version: over whatever the draft holds now
            var current = await groupedPageContentService.LoadGroupContentAsync(group, draft: true, cancellationToken);
            var result = await pageContentService.SaveConditionalContentAsync(group, content, current.ETag, cancellationToken);
            await pageHistory.RecordAsync(group, content, $"Restored {sha[..7]}", User, cancellationToken);

            return Ok(new { eTag = result.ETag });
        }

        /// <summary>Throws the page's unpublished changes away: it is what the storefront shows again.</summary>
        [HttpPost("discard")]
        [Authorize(ModuleConstants.Security.Permissions.Update)]
        public async Task<ActionResult> Discard([FromRoute] string groupId, CancellationToken cancellationToken = default)
        {
            var group = await AuthorizedGroupAsync(groupId);
            if (group == null)
            {
                return NotFound();
            }

            if (!group.Pages.Any(x => x.Status == Published))
            {
                return BadRequest(new { error = "This page has never been published, so there is no published version to go back to." });
            }

            var drafts = group.Pages.Where(x => x.Status == Draft).Select(x => x.Id).ToList();
            if (drafts.Count > 0)
            {
                await crudService.DeleteAsync(drafts);
                await pageHistory.RecordAsync(group, await pageHistory.LoadPublishedContentAsync(group, cancellationToken), "Discarded unpublished changes", User, cancellationToken);
            }

            return Ok();
        }

        private async Task<GroupedPageBuilderPage> AuthorizedGroupAsync(string groupId)
        {
            var group = await groupedPageService.GetByIdAsync(groupId);
            if (group == null)
            {
                return null;
            }

            var authorization = await authorizationService.AuthorizeAsync(User, group, new PageBuilderAuthorizationRequirement());
            return authorization.Succeeded ? group : null;
        }
    }
}
