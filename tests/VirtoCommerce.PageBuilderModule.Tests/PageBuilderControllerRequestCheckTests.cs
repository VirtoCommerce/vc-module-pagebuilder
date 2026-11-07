using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class PageBuilderControllerRequestCheckTests
    {
        private const string Store = "vccom";
        private const string Page = "/about-us.page";

        private static readonly Dictionary<string, Func<PageBuilderController, string, string, string, Task<ActionResult>>> Actions = new()
        {
            ["template"] = (c, store, type, path) => c.GetTemplate(store, null, path, type, draft: true),
            ["search"] = async (c, store, type, path) => (await c.Search(store, null, type, path)).Result,
            ["save"] = (c, store, type, path) => c.SaveTemplates(store, null, Files((type, path))),
            ["git/publish"] = (c, store, type, path) => c.GitPublish(store, path, type),
            ["git/promote"] = (c, store, type, path) => c.GitPromote(store, path, type),
            ["git/unpublish"] = (c, store, type, path) => c.GitUnpublish(store, path, type),
            ["git/publish-status"] = async (c, store, type, path) => (await c.GitPublishStatus(store, path, type)).Result,
            ["git/history"] = (c, store, type, path) => c.GitHistory(store, path, type),
            ["git/restore-version"] = (c, store, type, path) => c.GitRestoreVersion(store, path, type, new string('a', 40)),
            ["git/preview"] = (c, store, type, path) => c.GitPreview(store, path, type),
            ["git/discard-draft"] = (c, store, type, path) => c.GitDiscardDraft(store, path, type),
            ["discard-draft"] = (c, store, type, path) => c.DiscardDraft(store, path, type),
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
        public async Task WithoutAccessToTheStore_IsForbidden(string action)
        {
            var result = await Actions[action](Controller(storeAccess: false), Store, "pages", Page);

            Assert.IsType<ForbidResult>(result);
        }

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task WithoutAccessToTheStore_AnUnknownStore_IsForbiddenToo(string action)
        {
            var result = await Actions[action](Controller(storeAccess: false), "other-store", "pages", Page);

            Assert.IsType<ForbidResult>(result);
        }

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task WithoutAccessToTheStore_AMalformedRequest_IsForbidden(string action)
        {
            var result = await Actions[action](Controller(storeAccess: false), Store, "pages", "../../appsettings.page");

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
        [InlineData("themes", "default/config/builder_settings.json")]
        [InlineData("themes", "default/config/schemas/sections/banner.json")]
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
            // 404: past the check, the store is not on the git flow
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
        public async Task LiveSaveOfAPageWithoutGit_WithoutThePublishPermission_IsForbidden()
        {
            var controller = Controller(granted: [ModuleConstants.Security.Permissions.Update]);

            var result = await controller.SaveTemplates(Store, "default", Files(("pages", Page)), draft: false);

            Assert.IsType<ForbidResult>(result);
        }

        [Theory]
        [InlineData("git/publish")]
        [InlineData("git/promote")]
        [InlineData("git/unpublish")]
        [InlineData("git/publish-status")]
        [InlineData("git/history")]
        [InlineData("git/restore-version")]
        [InlineData("git/preview")]
        [InlineData("git/discard-draft")]
        [InlineData("git/copy")]
        [InlineData("git/legacy-drafts/delete")]
        public async Task PageActions_RefuseThemeFiles(string action)
        {
            // a page document's name, so that only the content type is wrong
            var result = await Actions[action](Controller(), Store, "themes", Page);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData("git/publish")]
        [InlineData("git/promote")]
        [InlineData("git/unpublish")]
        [InlineData("git/publish-status")]
        [InlineData("git/history")]
        [InlineData("git/restore-version")]
        [InlineData("git/preview")]
        [InlineData("git/discard-draft")]
        [InlineData("git/copy")]
        public async Task PageActions_RefuseAPathThatIsNotAPage(string action)
        {
            var result = await Actions[action](Controller(), Store, "pages", "/README.md");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData("main")]
        [InlineData("designer/alice/about-us-page-1a2b3c4")]
        public async Task Template_AtARefThatIsNotACommitSha_IsBadRequest(string gitRef)
        {
            var result = await Controller().GetTemplate(Store, null, Page, "pages", draft: true, gitRef);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Preview_AtARefThatIsNotACommitSha_IsBadRequest()
        {
            var result = await Controller().GitPreview(Store, Page, "pages", "main");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData("a/b")]
        [InlineData("..\\x")]
        [InlineData("a*")]
        [InlineData("a\0")]
        public async Task Search_ForAKeywordStorageReadsAsAPath_IsBadRequest(string keyword)
        {
            var result = await Controller().Search(Store, null, "pages", null, keyword: keyword);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Theory]
        [InlineData(0, null)]
        [InlineData(101, null)]
        [InlineData(null, "not a cursor")]
        public async Task History_PagedOutsideWhatGitHubAnswers_IsBadRequest(int? take, string after)
        {
            var result = await Controller().GitHistory(Store, Page, "pages", take, after);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SchemaOfAnUnknownKind_IsBadRequest()
        {
            var result = await Controller().GetSchemaByKey(Store, "default", "pageSettings", "text");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ABodyThatDidNotBind_IsBadRequest()
        {
            // MVC records the failure and leaves the parameter null; without [ApiController] nothing answers it for us
            var controller = Controller();
            controller.ModelState.AddModelError("request.Paths", "Error converting value \"/a.page-draft\" to type 'System.Collections.Generic.IList`1[System.String]'.");

            var result = await controller.DeleteLegacyDrafts(Store, "pages", null, null);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Save_WithoutAccessToTheStore_IsForbiddenBeforeTheBodyIsRead()
        {
            var result = await Controller(storeAccess: false).SaveTemplates(Store, "default", new PageBuilderController.SaveFilesModel());

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task LiveSaveWithGit_WithoutThePublishPermission_IsForbidden_ThemeSettingsToo()
        {
            var controller = Controller(granted: [ModuleConstants.Security.Permissions.Update], gitFlow: true);

            var result = await controller.SaveTemplates(Store, "default", Files(("themes", "default/config/settings_data.json")), draft: false);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task LiveSave_WithoutThePublishPermission_IsForbiddenBeforeTheFilesAreChecked()
        {
            var controller = Controller(granted: [ModuleConstants.Security.Permissions.Update]);

            var result = await controller.SaveTemplates(Store, "default", Files(("pages", "../../appsettings.page")), draft: false);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Save_OfAPageWithAMalformedSettingsType_IsBadRequest()
        {
            var value = new PageBuilderController.SaveFilesModel
            {
                Files = JsonConvert.SerializeObject(new[]
                {
                    new { path = Page, type = "pages", content = new { settings = new { type = new { } }, content = Array.Empty<object>() } },
                }),
            };

            var result = await Controller().SaveTemplates(Store, "default", value, draft: true);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        private static PageBuilderController.SaveFilesModel Files(params (string Type, string Path)[] files) =>
            new()
            {
                Files = JsonConvert.SerializeObject(files.Select(file => new PageBuilderController.SaveFileModel
                {
                    Type = file.Type,
                    Path = file.Path,
                    Content = new { settings = new { type = "settings" }, content = Array.Empty<object>() },
                })),
            };

        private static PageBuilderController Controller(bool storeAccess = true, string[] granted = null, IAuthorizationService authorizationService = null, bool gitFlow = false)
        {
            // Nothing past the check is wired up, so an action that reaches storage first fails here.
            var filePageDrafts = TestServices.FilePageDrafts(null, null, new FakeGitContentPolicy(enabled: gitFlow), null, null, new FakeGitContentProvisioner(), new FakeStoreService(new Store { Id = Store }), null, null, null, null);
            var controller = new PageBuilderController(
                storeService: new FakeStoreService(new Store { Id = Store }),
                pathResolver: null,
                blobContentStorageProviderFactory: null,
                publishingService: null,
                eventPublisher: null,
                gitContentOptions: null,
                gitHubConnectionService: null,
                gitContentPolicy: new FakeGitContentPolicy(enabled: gitFlow),
                gitContentRepository: null,
                gitContentHistory: null,
                gitContentPublisher: null,
                gitLivePageService: null,
                settingsManager: null,
                authorizationService: authorizationService ?? new ScopedAuthorizationService(storeAccess, granted),
                distributedLock: new InProcessDistributedLock(),
                requestValidator: new PageBuilderContentRequestValidator(new PageBuilderStoreRequestValidator(new FakeStoreService(new Store { Id = Store })), new PageBuilderContentFileValidator()),
                contentHistory: new NoContentHistory(),
                themeDraftService: null,
                groupedPageService: null,
                sharedComponentPlacementValidator: null,
                designerContentService: TestServices.Designer(new NoContentHistory(), filePageDrafts, null, null, null, null, null),
                contentPermissionService: TestServices.Permissions(authorizationService ?? new ScopedAuthorizationService(storeAccess, granted)),
                filePageDraftService: filePageDrafts);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "john")], "test")) },
            };

            return controller;
        }

        private sealed class ScopedAuthorizationService(bool storeAccess, string[] granted) : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                Result(requirements.All(requirement => requirement is not PageBuilderAuthorizationRequirement) || storeAccess);

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
                Result(granted == null || granted.Contains(policyName));

            private static Task<AuthorizationResult> Result(bool allowed) =>
                Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }
    }
}
