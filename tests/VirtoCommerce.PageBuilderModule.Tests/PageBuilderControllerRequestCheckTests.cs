using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// Every action of <see cref="PageBuilderController"/> refuses a request before it reaches storage: a
    /// content type or a path that could leave the store's content folder is a 400, an unknown store is a
    /// 400, and a caller without the permission or without access to the store is a 403.
    /// </summary>
    public class PageBuilderControllerRequestCheckTests
    {
        private const string Store = "vccom";
        private const string Page = "/about-us.page";

        private static readonly Dictionary<string, Func<PageBuilderController, string, string, string, Task<ActionResult>>> Actions = new()
        {
            ["template"] = (c, store, type, path) => c.GetTemplate(store, null, path, type, draft: true),
            ["search"] = (c, store, type, path) => c.Search(store, null, type, path),
            ["save"] = (c, store, type, path) => c.SaveTemplates(store, null, Files((type, path))),
            ["git/publish"] = (c, store, type, path) => c.GitPublish(store, path, type),
            ["git/promote"] = (c, store, type, path) => c.GitPromote(store, path, type),
            ["git/unpublish"] = (c, store, type, path) => c.GitUnpublish(store, path, type),
            ["git/publish-status"] = (c, store, type, path) => c.GitPublishStatus(store, path, type),
            ["git/history"] = (c, store, type, path) => c.GitHistory(store, path, type),
            ["git/restore-version"] = (c, store, type, path) => c.GitRestoreVersion(store, path, type, new string('a', 40)),
            ["git/preview"] = (c, store, type, path) => c.GitPreview(store, path, type),
            ["git/discard-draft"] = (c, store, type, path) => c.GitDiscardDraft(store, path, type),
            ["git/copy"] = (c, store, type, path) => c.GitCopyPage(store, type, path, null),
            ["git/legacy-draft"] = (c, store, type, path) => c.GetLegacyDraft(store, type, path, null),
            ["git/legacy-drafts/delete"] = (c, store, type, path) =>
                c.DeleteLegacyDrafts(store, type, null, new LegacyDraftsDeleteRequest { Paths = [path + "-draft"] }),
        };

        private static readonly Dictionary<string, Func<PageBuilderController, string, string, Task<ActionResult>>> ThemeActions = new()
        {
            ["settings"] = (c, store, theme) => c.GetSettings(store, theme),
            ["templates"] = (c, store, theme) => c.GetTemplates(store, theme),
            ["objects"] = (c, store, theme) => c.GetObjects(store, theme),
            ["sections"] = (c, store, theme) => c.GetSectionsSettings(store, theme),
            ["schemas"] = (c, store, theme) => c.GetSchemasCatalog(store, theme),
            ["schemas/{kind}/{key}"] = (c, store, theme) => c.GetSchemaByKey(store, theme, "sections", "text"),
            ["git/legacy-drafts"] = (c, store, theme) => c.GetLegacyDrafts(store, "pages", theme),
        };

        public static TheoryData<string> ActionNames => [.. Actions.Keys];

        public static TheoryData<string> ThemeActionNames => [.. ThemeActions.Keys];

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task UnknownContentType_IsBadRequest(string action)
        {
            var result = await Actions[action](Controller(), Store, "../../../etc", Page);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task PathOutsideTheContentFolder_IsBadRequest(string action)
        {
            var result = await Actions[action](Controller(), Store, "pages", "../../appsettings.page");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task UnknownStore_IsBadRequest(string action)
        {
            var result = await Actions[action](Controller(), "other-store", "pages", Page);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task WithoutThePermission_IsForbidden(string action)
        {
            var result = await Actions[action](Controller(permissions: false), Store, "pages", Page);

            Assert.IsType<ForbidResult>(result);
        }

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task WithoutAccessToTheStore_IsForbidden(string action)
        {
            var result = await Actions[action](Controller(storeAccess: false), Store, "pages", Page);

            Assert.IsType<ForbidResult>(result);
        }

        [Theory]
        [MemberData(nameof(ThemeActionNames))]
        public async Task ThemeOutsideTheThemesFolder_IsBadRequest(string action)
        {
            var result = await ThemeActions[action](Controller(), Store, "../other-store");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [MemberData(nameof(ThemeActionNames))]
        public async Task ThemeActions_WithoutAccessToTheStore_AreForbidden(string action)
        {
            var result = await ThemeActions[action](Controller(storeAccess: false), Store, "default");

            Assert.IsType<ForbidResult>(result);
        }

        [Theory]
        [MemberData(nameof(ThemeActionNames))]
        public async Task ThemeActions_ForAnUnknownStore_AreBadRequest(string action)
        {
            var result = await ThemeActions[action](Controller(), "other-store", "default");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SchemaKeyWithAPath_IsBadRequest()
        {
            var result = await Controller().GetSchemaByKey(Store, "default", "sections", "../../secrets");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData("pages", "about-us.json")]
        [InlineData("themes", "default/config/settings_data.page")]
        [InlineData("themes", "default/../../config.json")]
        [InlineData("assets", "x.page")]
        [InlineData(null, "about-us.page")]
        public async Task Save_AFileTheDesignerDoesNotWrite_IsBadRequest(string type, string path)
        {
            var result = await Controller().SaveTemplates(Store, "default", Files((type, path)));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData("/about-us.html")]
        [InlineData("/about-us.json")]
        public async Task Copy_ToAFileTheDesignerDoesNotWrite_IsBadRequest(string destPath)
        {
            var result = await Controller().GitCopyPage(Store, "pages", Page, destPath);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Copy_ToAPage_PassesTheCheck()
        {
            // the store is not on the git flow: a request that got past the check is answered 404
            var result = await Controller().GitCopyPage(Store, "pages", Page, "/about-us_2.page");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Save_WithoutFiles_IsBadRequest()
        {
            var result = await Controller().SaveTemplates(Store, "default", new PageBuilderController.SaveFilesModel());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task PublishWithoutGit_WithoutThePublishPermission_IsForbidden()
        {
            var controller = Controller(granted: [ModuleConstants.Security.Permissions.Read]);

            var result = await controller.GitPublish(Store, Page, "pages");

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UnpublishWithoutGit_WithoutThePublishPermission_IsForbidden()
        {
            var controller = Controller(granted: [ModuleConstants.Security.Permissions.Read]);

            var result = await controller.GitUnpublish(Store, Page, "pages");

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task LiveSaveOfAPageWithoutGit_WithoutThePublishPermission_IsForbidden()
        {
            var controller = Controller(granted: [ModuleConstants.Security.Permissions.Update]);

            var result = await controller.SaveTemplates(Store, "default", Files(("pages", Page)), draft: false);

            Assert.IsType<ForbidResult>(result);
        }

        private static PageBuilderController.SaveFilesModel Files(params (string Type, string Path)[] files) =>
            new()
            {
                Files = JsonConvert.SerializeObject(files.Select(file => new PageBuilderController.SaveFileModel
                {
                    Type = file.Type,
                    Path = file.Path,
                    Content = new { },
                })),
            };

        private static PageBuilderController Controller(bool permissions = true, bool storeAccess = true, string[] granted = null)
        {
            // Nothing past the request check is wired up: an action that reaches storage before refusing
            // fails here with a NullReferenceException instead of passing.
            var controller = new PageBuilderController(
                storeService: new FakeStoreService(new Store { Id = Store }),
                pathResolver: null,
                blobContentStorageProviderFactory: null,
                publishingService: null,
                eventPublisher: null,
                gitContentOptions: null,
                gitContentPolicy: new FixedGitContentPolicy(enabled: false),
                gitContentRepository: null,
                gitContentHistory: null,
                gitContentPublisher: null,
                settingsManager: null,
                authorizationService: new ScopedAuthorizationService(permissions, storeAccess, granted));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "john")], "test")) },
            };

            return controller;
        }

        private sealed class FixedGitContentPolicy(bool enabled) : IGitContentPolicy
        {
            public Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default) => Task.FromResult(enabled);
        }

        /// <summary>Grants permissions and store access separately, the two questions the check asks.</summary>
        private sealed class ScopedAuthorizationService(bool permissions, bool storeAccess, string[] granted) : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                Result(requirements.All(requirement => requirement is not PageBuilderAuthorizationRequirement) || storeAccess);

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
                Result(permissions && (granted == null || granted.Contains(policyName)));

            private static Task<AuthorizationResult> Result(bool allowed) =>
                Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }
    }
}
