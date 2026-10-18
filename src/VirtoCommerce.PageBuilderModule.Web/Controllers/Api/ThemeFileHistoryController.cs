using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    /// <summary>
    /// A theme file's version history (<see cref="ThemeFileHistory"/>) — a template, the template rules, a section
    /// group: its changes, one change's content, and going back to a change. The file is named as the theme storage
    /// names it ("default/templates/product.json"). 404 for a store without history.
    /// </summary>
    [Route("api/pagebuilder/theme-history")]
    [Authorize]
    public class ThemeFileHistoryController(
        IContentPathResolver pathResolver,
        IBlobContentStorageProviderFactory blobContentStorageProviderFactory,
        IPublishingService publishingService,
        IEventPublisher eventPublisher,
        ThemeFileHistory history) : Controller
    {
        private const string Themes = "themes";

        [HttpGet("")]
        public async Task<ActionResult<GroupedPageHistoryResult>> GetHistory([FromQuery] string storeId, [FromQuery] string path, [FromQuery] string after = null,
            [FromQuery] int take = 20, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(storeId) || !ThemeFileHistory.IsThemePath(path))
            {
                return NotFound();
            }

            try
            {
                var published = await PageBuilderController.ReadTextAsync(Storage(storeId, path), publishingService.GetRelativeDraftUrl(path, false));
                var versions = await history.GetAsync(storeId, path, published, after, take, cancellationToken);
                return versions == null ? NotFound() : Ok(versions);
            }
            catch (Exception failure) when (failure is HttpRequestException or InvalidOperationException)
            {
                return BadRequest(new { error = failure.Message });
            }
        }

        /// <summary>The file's content at a change, to preview it.</summary>
        [HttpGet("{sha}")]
        public async Task<ActionResult> GetVersion([FromRoute] string sha, [FromQuery] string storeId, [FromQuery] string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(storeId) || !ThemeFileHistory.IsThemePath(path) || !GitCommitSha.IsSha(sha))
            {
                return NotFound();
            }

            var content = await history.ReadAsync(storeId, path, sha, cancellationToken);
            return content == null ? NotFound() : Content(content, "application/json");
        }

        /// <summary>
        /// Makes a change's content the file's draft again — a new change on top, so nothing after it is lost;
        /// publishing it is a separate step, as after any save.
        /// </summary>
        [HttpPost("{sha}/restore")]
        public async Task<ActionResult> Restore([FromRoute] string sha, [FromQuery] string storeId, [FromQuery] string path, [FromQuery] string version = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(storeId) || !ThemeFileHistory.IsThemePath(path) || !GitCommitSha.IsSha(sha))
            {
                return NotFound();
            }

            var content = await history.ReadAsync(storeId, path, sha, cancellationToken);
            if (content == null)
            {
                return NotFound();
            }

            // the change's bytes as they are: re-serializing could change them, and the published change is told by them
            var storage = Storage(storeId, path);
            await using (var stream = await storage.OpenWriteAsync(publishingService.GetRelativeDraftUrl(path, true)))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(content);
            }

            await eventPublisher.Publish(new PageBuilderContentChangedEvent(Themes,
                [new GenericChangedEntry<FileEntity>(new FileEntity { Path = path, Type = Themes }, EntryState.Modified)]), cancellationToken);
            await history.RecordAsync(storeId, path, content, $"Restored {SiteSave.Version(version, sha)}", User, SiteSave.Of(Request), cancellationToken);

            return Ok();
        }

        // the store's theme storage; the path starts with the theme's folder, as the designer's saves name it
        private IBlobContentStorageProvider Storage(string storeId, string path) =>
            blobContentStorageProviderFactory.CreateProvider(pathResolver.GetContentBasePath(Themes, storeId, path.Split('/')[0]));
    }
}
