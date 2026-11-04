using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// Theme files are blobs on either flow: saving or discarding one asks GitHub nothing — not whose the store's
    /// repository is, and not whether GitHub answers at all.
    /// </summary>
    public class ThemeFilesStayBlobsTests
    {
        private const string Path = "default/sections/header-group.json";

        private readonly FakeLiveStorage _live = new();
        private readonly FakeGitContentOwnership _ownership = new("another-store");

        [Fact]
        public async Task A_theme_save_of_a_store_on_git_asks_GitHub_nothing()
        {
            var files = new PageBuilderController.SaveFilesModel
            {
                Files = JsonConvert.SerializeObject(new[] { new { path = Path, type = "themes", content = new { content = Array.Empty<object>() } } }),
            };

            Assert.IsType<OkObjectResult>(await Builder().SaveTemplates("s1", "default", files, draft: true));

            Assert.True(_live.Files.ContainsKey($"{Path}-draft"));
            Assert.Empty(_ownership.Asked);
        }

        [Fact]
        public async Task Discarding_a_theme_draft_of_a_store_on_git_asks_GitHub_nothing()
        {
            _live.Files[Path] = "{}";
            _live.Files[$"{Path}-draft"] = "{}";

            Assert.IsType<NoContentResult>(await Builder().DiscardDraft("s1", Path, "themes", "default"));

            Assert.False(_live.Files.ContainsKey($"{Path}-draft"));
            Assert.Empty(_ownership.Asked);
        }

        private PageBuilderController Builder()
        {
            var options = Options.Create(new GitContentOptions { Enabled = true, Organization = "acme", Token = "t0ken", BaseBranch = "main" });
            var controller = new PageBuilderController(
                storeService: _live.StoreService,
                pathResolver: _live,
                blobContentStorageProviderFactory: _live,
                publishingService: _live,
                eventPublisher: _live,
                gitContentOptions: options,
                gitContentPolicy: new GitContentPolicy(options, new FakeSettingsManager(true), _ownership),
                gitContentRepository: null,
                gitContentHistory: null,
                gitContentPublisher: null,
                gitContentProvisioner: new FakeGitContentProvisioner(),
                settingsManager: null,
                authorizationService: new FakeAuthorizationService(true),
                distributedLock: new InProcessDistributedLock());
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test")) },
            };
            return controller;
        }
    }
}
