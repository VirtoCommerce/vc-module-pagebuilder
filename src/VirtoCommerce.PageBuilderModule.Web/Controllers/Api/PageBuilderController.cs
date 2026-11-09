using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.ContentModule.Data.Extensions;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Extensions;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using StorePermissions = VirtoCommerce.StoreModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    [Route("api/pagebuilder")]
    [Authorize]
    [RepositoryOwnershipConflict]
    [GitContentFailure]
    public class PageBuilderController(IStoreService storeService,
            IContentPathResolver pathResolver,
            IBlobContentStorageProviderFactory blobContentStorageProviderFactory,
            IPublishingService publishingService,
            IEventPublisher eventPublisher,
            IOptions<GitContentOptions> gitContentOptions,
            IGitHubConnectionService gitHubConnectionService,
            IGitContentPolicy gitContentPolicy,
            IGitContentRepository gitContentRepository,
            IGitContentHistory gitContentHistory,
            IGitContentPublisher gitContentPublisher,
            IGitLivePageService gitLivePageService,
            ISettingsManager settingsManager,
            IAuthorizationService authorizationService,
            IDistributedLock distributedLock,
            IValidator<PageBuilderContentRequest> requestValidator,
            IContentHistory contentHistory,
            IThemeDraftService themeDraftService,
            IGroupedPageService groupedPageService,
            IPageBuilderSharedComponentPlacementValidator sharedComponentPlacementValidator,
            IDesignerContentService designerContentService,
            IContentPermissionService contentPermissionService,
            IFilePageDraftService filePageDraftService,
            IValidator<DesignerSaveRequest> designerSaveValidator,
            IValidator<DesignerDiscardRequest> designerDiscardValidator
            )
        : Controller
    {
        private const string Themes = "themes";
        // The content types the git flow covers are pages and blogs — the same .page files, blogs living
        // in a subfolder (see GitPageLocation.ContentPath). Themes, schemas and settings stay in blob
        // storage. PagesContentType is the default assumed when a caller sends no type at all.
        private const string PagesContentType = "pages";
        private const string DefaultPreviewPath = "/designer-preview";
        // How a client names the two flows in the publish-status answer.
        private const string GitFlow = "git";
        private const string BlobFlow = "blob";
        private const string DefaultTheme = ModuleConstants.Theme.DefaultThemeName;
        private const string JsonContentType = "application/json";
        private const string JsonExtension = ".json";
        private const string SchemaKindSections = ModuleConstants.SchemaKinds.Sections;
        private const string SchemaKindTemplates = ModuleConstants.SchemaKinds.Templates;
        private const string SchemaKindBlocks = ModuleConstants.SchemaKinds.Blocks;
        private const string SchemaKindObjects = ModuleConstants.SchemaKinds.Objects;
        private const string SchemaKindShared = ModuleConstants.SchemaKinds.Shared;
        // The blob name of a draft in the flow that came before git: the page's name with this appended.
        // Only the legacy-draft cleanup deals in these; everywhere else GitPageLocation strips the suffix.
        private const string DraftSuffix = ModuleConstants.DraftSuffix;
        private const string FolderEntryType = "folder";
        private const int MaxLegacyDraftPageSize = 200;
        private const string LegacyDraftOnlyCopy = "only-copy";
        private const string LegacyDraftHasChanges = "has-changes";
        // How many "_N" names a duplicate will try before asking the caller to pick one. A page with
        // this many copies is a naming problem, not a paging problem.
        private const int MaxCopyIndex = 100;
        private const string OnlyCopyError = "Nothing is published: the draft is the only copy.";
        private const string CatalogRequiredError = "Theme files are checked against the storefront's schema catalog: name it with catalogHash (preview the storefront first).";
        private const string SaveAction = "save";


        [HttpGet]
        [Route("template")]
        public async Task<ActionResult> GetTemplate(string storeId, string theme, string path, string type, bool draft = false, [FromQuery(Name = "ref")] string gitRef = null)
        {
            var request = new PageBuilderContentRequest { StoreId = storeId, ContentType = type, Theme = theme, Paths = PathsOf(path), Ref = gitRef };
            if (await CheckRequestAsync(request) is { } refused)
            {
                return refused;
            }

            // With the git flow on, a page is read from git: the caller's URL does not change, because
            // resolving "this editor's draft, else what is published" is the server's job.
            var repository = GitPageLocation.IsPageContent(type) && !path.IsNullOrEmpty()
                ? await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted)
                : null;
            if (repository != null)
            {
                var fromGit = await ReadPageFromGitAsync(repository, type, path, draft, gitRef);
                if (fromGit != null)
                {
                    return PageContent(fromGit);
                }

                if (!string.IsNullOrEmpty(gitRef))
                {
                    // an exact commit was asked for; blob storage cannot answer that question
                    return NotFound(new { templatePath = path, gitRef });
                }

                // Not in the repository — fall through to blob storage. A store that opts in keeps every
                // page it had: those only reach git at their first save from the designer, and until then
                // the blob copy is the page. Refusing to read it would make opting in look like the
                // builder lost the store's content.
            }

            return await ReadTemplateFromBlobAsync(storeId, theme, path, type, draft);
        }

        private async Task<ActionResult> ReadTemplateFromBlobAsync(string storeId, string theme, string path, string type, bool draft)
        {
            var basePath = GetContentBasePath(storeId, type, theme);
            if (!path.IsNullOrEmpty())
            {
                var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);

                var blobInfo = await storageProvider.GetBlobInfoAsync(publishingService.GetRelativeDraftUrl(path, draft));
                if (blobInfo == null && draft)
                {
                    blobInfo = await storageProvider.GetBlobInfoAsync(publishingService.GetRelativeDraftUrl(path, false));
                }

                if (blobInfo != null)
                {
                    var stream = await storageProvider.OpenReadAsync(blobInfo.RelativeUrl);
                    return File(stream, MimeTypeResolver.ResolveContentType(blobInfo.Name));
                }
            }

            return NotFound(new
            {
                basePath,
                templatePath = path
            });
        }

        /// <summary>
        /// Reads a page out of the content repository, or <c>null</c> when the repository does not have
        /// it. Without an explicit ref: the editor's draft of this page when they have one, otherwise the
        /// published version. That fallback is what keeps the contract of the blob-backed endpoint — "the
        /// draft, else what is live" — intact, so the callers of this URL never learn that pages moved to
        /// git.
        /// </summary>
        private async Task<string> ReadPageFromGitAsync(string repository, string type, string path, bool draft, string gitRef)
        {
            var location = GitLocation(repository, type, path);

            if (!string.IsNullOrEmpty(gitRef))
            {
                // an exact commit — what a preview link points at
                return await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, gitRef, HttpContext.RequestAborted);
            }

            if (draft)
            {
                var onBranch = await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, location.Branch, HttpContext.RequestAborted);
                if (onBranch != null)
                {
                    return onBranch;
                }
            }

            return await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, await GetBaseBranchAsync(), HttpContext.RequestAborted);
        }

        private async Task<string> GetBaseBranchAsync() => (await gitHubConnectionService.GetConnectionAsync(HttpContext.RequestAborted)).BaseBranch;

        private GitPageFileLocation GitLocation(string repository, string type, string path) =>
            gitContentOptions.Value.GetPageLocation(repository, type, path, User?.Identity?.Name);

        private ContentResult PageContent(string json) => Content(json, JsonContentType);

        [HttpGet]
        [Route("settings")]
        public async Task<ActionResult> GetSettings(string storeId, string theme)
        {
            if (await CheckRequestAsync(storeId, Themes, theme) is { } refused)
            {
                return refused;
            }

            var themeName = await GetCurrentThemeName(storeId, theme);
            var filePath = $"{themeName}/config/builder_settings.json";
            var basePath = GetContentBasePath(storeId, Themes, themeName);
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);

            var blobInfo = await storageProvider.GetBlobInfoAsync(filePath);
            var gitFlow = await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted);

            if (blobInfo == null)
            {
                return Content(gitFlow ? GitBuilderDescriptors().ToString(Formatting.None) : "{}", JsonContentType);
            }

            if (!gitFlow)
            {
                var stream = await storageProvider.OpenReadAsync(blobInfo.RelativeUrl);
                return File(stream, MimeTypeResolver.ResolveContentType(blobInfo.Name));
            }

            // The builder overlays this response on top of its bundled data/settings.json, server wins.
            // That is how a store on the git flow gets git urls for publishing without anyone editing the
            // app's static config — and how a store that is not on it keeps the bytes it has always got.
            var settings = await ReadJsonBlobAsync(storageProvider, blobInfo.RelativeUrl);
            settings.Merge(GitBuilderDescriptors(), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });

            return Content(settings.ToString(Formatting.None), JsonContentType);
        }

        /// <summary>
        /// The publish-related request descriptors for a store on the git flow. <c>unpublish</c> is one of
        /// them: with pages in git, taking a page down is deleting the file from the production branch, so
        /// it ships the same way a publish does — a commit on the work branch and a merge.
        /// <para>
        /// The same mechanism switches version history on: the panel exists only for a store whose pages
        /// are in git, and it learns where to ask from here rather than from the app's bundled config.
        /// </para>
        /// </summary>
        public static JObject GitBuilderDescriptors()
        {
            const string storeIdArg = "storeId={{location.params.storeId}}";

            // Every url carries the content type: pages and blogs are the same kind of file in two
            // folders, and the server cannot tell them apart from the path alone.
            const string pageArgs = "path={{path}}&type={{type}}";

            return new JObject
            {
                ["publish"] = new JObject
                {
                    ["status"] = $"/api/pagebuilder/git/publish-status?{storeIdArg}&{pageArgs}",
                    ["publish"] = new JObject
                    {
                        ["url"] = $"/api/pagebuilder/git/publish?{storeIdArg}&{pageArgs}",
                        ["method"] = "POST",
                    },
                    ["unpublish"] = new JObject
                    {
                        ["url"] = $"/api/pagebuilder/git/unpublish?{storeIdArg}&{pageArgs}",
                        ["method"] = "POST",
                    },
                    // Offered whether or not a release branch is configured; the status endpoint reports
                    // production as null when there is none, and the client shows no production stage at
                    // all. Gating the descriptor as well would mean two places to keep in agreement.
                    ["promote"] = new JObject
                    {
                        ["url"] = $"/api/pagebuilder/git/promote?{storeIdArg}&{pageArgs}",
                        ["method"] = "POST",
                    },
                },
                ["externalPreview"] = new JObject
                {
                    ["url"] = $"/api/pagebuilder/git/preview?{storeIdArg}&{pageArgs}",
                },
                ["history"] = new JObject
                {
                    ["url"] = $"/api/pagebuilder/git/history?{storeIdArg}&{pageArgs}",
                    // a version is addressed by its sha, which the panel substitutes per row
                    ["preview"] = new JObject
                    {
                        ["url"] = $"/api/pagebuilder/git/preview?{storeIdArg}&{pageArgs}&ref={{{{sha}}}}",
                    },
                    ["restore"] = new JObject
                    {
                        ["url"] = $"/api/pagebuilder/git/restore-version?{storeIdArg}&{pageArgs}&sha={{{{sha}}}}",
                        ["method"] = "POST",
                    },
                },
            };
        }

        private static async Task<JObject> ReadJsonBlobAsync(IBlobContentStorageProvider storageProvider, string relativeUrl)
        {
            await using var stream = await storageProvider.OpenReadAsync(relativeUrl);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            return string.IsNullOrWhiteSpace(json) ? [] : JObject.Parse(json);
        }

        [HttpGet]
        [Route("templates")]
        public async Task<ActionResult> GetTemplates(string storeId, string theme)
        {
            if (await CheckRequestAsync(storeId, Themes, theme) is { } refused)
            {
                return refused;
            }

            var result = await GetSettingsFilesFromFolder(storeId, theme, "templates");
            return Content(result, JsonContentType);
        }

        [HttpGet]
        [Route("objects")]
        public async Task<ActionResult> GetObjects(string storeId, string theme)
        {
            if (await CheckRequestAsync(storeId, Themes, theme) is { } refused)
            {
                return refused;
            }

            var result = await GetSettingsFilesFromFolder(storeId, theme, SchemaKindObjects);
            return Ok(result);
        }

        [HttpGet]
        [Route("sections")]
        public async Task<ActionResult> GetSectionsSettings(string storeId, string theme)
        {
            if (await CheckRequestAsync(storeId, Themes, theme) is { } refused)
            {
                return refused;
            }

            var sections = await GetSettingsFilesFromFolder(storeId, theme, SchemaKindSections);
            var blocks = await GetSettingsFilesFromFolder(storeId, theme, SchemaKindBlocks);
            var objects = await GetSettingsFilesFromFolder(storeId, theme, SchemaKindObjects);
            var shared = await GetSettingsFilesFromFolder(storeId, theme, SchemaKindShared);
            return Content($"{{ \"{SchemaKindSections}\": {sections}, \"{SchemaKindBlocks}\": {blocks}, \"{SchemaKindObjects}\": {objects}, \"{SchemaKindShared}\": {shared} }}", JsonContentType);
        }

        [HttpGet]
        [Route("schemas")]
        public async Task<ActionResult> GetSchemasCatalog(string storeId, string theme)
        {
            if (await CheckRequestAsync(storeId, Themes, theme) is { } refused)
            {
                return refused;
            }

            var catalog = new JObject
            {
                [SchemaKindSections] = await GetSchemasCatalogForFolder(storeId, theme, SchemaKindSections, filterInternal: true),
                [SchemaKindTemplates] = await GetSchemasCatalogForFolder(storeId, theme, SchemaKindTemplates, filterInternal: true),
                [SchemaKindBlocks] = await GetSchemasCatalogForFolder(storeId, theme, SchemaKindBlocks, filterInternal: true),
                [SchemaKindObjects] = await GetSchemasCatalogForFolder(storeId, theme, SchemaKindObjects, filterInternal: true),
                [SchemaKindShared] = await GetSchemasCatalogForFolder(storeId, theme, SchemaKindShared, filterInternal: false),
            };
            return Content(catalog.ToString(Formatting.None), JsonContentType);
        }

        [HttpGet]
        [Route("schemas/{kind}/{key}")]
        public async Task<ActionResult> GetSchemaByKey(string storeId, string theme, string kind, string key)
        {
            var request = new PageBuilderContentRequest { StoreId = storeId, ContentType = Themes, Theme = theme, SchemaKind = kind, SchemaKey = key };
            if (await CheckRequestAsync(request) is { } refused)
            {
                return refused;
            }

            // Underscore-prefixed keys are theme-internal; only `shared` exposes them (e.g. _sections, _blocks).
            if (kind != SchemaKindShared && key.StartsWith('_'))
            {
                return NotFound(new { kind, key });
            }

            var themeName = await GetCurrentThemeName(storeId, theme);
            var schemasFolder = $"{themeName}/config/schemas/{kind}";
            var basePath = GetContentBasePath(storeId, Themes, themeName);
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);
            var allFiles = await storageProvider.SearchAsync(schemasFolder, null);
            var file = allFiles.Results.FirstOrDefault(x =>
                x.Type != "folder" &&
                x.Name.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileNameWithoutExtension(x.Name).Equals(key, StringComparison.OrdinalIgnoreCase));

            if (file == null)
            {
                return NotFound(new { kind, key });
            }

            var content = GetContent(file, storageProvider);

            if (kind == SchemaKindTemplates)
            {
                content = await MergeStaticSectionsIntoTemplateAsync(content, storeId, themeName);
            }

            return Content(content, JsonContentType);
        }


        internal static bool IsStaticEntry(JToken entry)
        {
            var token = entry?["static"];
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }
            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }
            if (token.Type == JTokenType.String)
            {
                var value = token.Value<string>();
                return value == "top" || value == "bottom";
            }
            return false;
        }

        internal static string MergeStaticSectionsIntoTemplate(string templateJson, IReadOnlyDictionary<string, string> sectionSchemasByKey)
        {
            JObject template;
            try
            {
                template = JObject.Parse(templateJson);
            }
            catch
            {
                return templateJson;
            }

            // Template's `sections` filter (if present and non-empty) restricts which sections apply
            // to this template — for both regular and static. Missing/empty means "all sections".
            HashSet<string> allowedKeys = null;
            if (template["sections"] is JArray sectionsFilter && sectionsFilter.Count > 0)
            {
                allowedKeys = new HashSet<string>(
                    sectionsFilter.OfType<JValue>().Select(v => v.Value?.ToString()).Where(s => s != null),
                    StringComparer.OrdinalIgnoreCase);
            }

            if (template["settings"] is not JArray templateSettings)
            {
                templateSettings = [];
                template["settings"] = templateSettings;
            }

            foreach (var (sectionKey, sectionJson) in sectionSchemasByKey)
            {
                if (sectionKey.StartsWith('_'))
                {
                    continue;
                }
                if (allowedKeys != null && !allowedKeys.Contains(sectionKey))
                {
                    continue;
                }
                AppendStaticSectionFields(sectionJson, templateSettings);
            }

            return template.ToString(Formatting.None);
        }

        private async Task<string> MergeStaticSectionsIntoTemplateAsync(string templateJson, string storeId, string themeName)
        {
            var sectionsFolder = $"{themeName}/config/schemas/{SchemaKindSections}";
            var basePath = GetContentBasePath(storeId, Themes, themeName);
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);
            var sectionFiles = (await storageProvider.SearchAsync(sectionsFolder, null)).Results
                .Where(x => x.Type != "folder" && x.Name.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase));

            var schemas = new Dictionary<string, string>();
            foreach (var file in sectionFiles)
            {
                try
                {
                    schemas[Path.GetFileNameWithoutExtension(file.Name)] = GetContent(file, storageProvider);
                }
                catch
                {
                    // Skip unreadable section files.
                }
            }

            return MergeStaticSectionsIntoTemplate(templateJson, schemas);
        }

        private static void AppendStaticSectionFields(string sectionJson, JArray templateSettings)
        {
            try
            {
                var sectionSchema = JObject.Parse(sectionJson);
                if (!IsStaticEntry(sectionSchema) || sectionSchema["settings"] is not JArray sectionSettings)
                {
                    return;
                }
                foreach (var field in sectionSettings)
                {
                    templateSettings.Add(field.DeepClone());
                }
            }
            catch
            {
                // Skip unparseable section files.
            }
        }

        [HttpGet]
        [Route("search")]
        public async Task<ActionResult<string>> Search(string storeId, string theme, string type, string folder, string pattern = null, string keyword = null)
        {
            var request = new PageBuilderContentRequest { StoreId = storeId, ContentType = type, Theme = theme, Paths = PathsOf(folder), Keyword = keyword };
            if (await CheckRequestAsync(request) is { } refused)
            {
                return refused;
            }

            var basePath = GetContentBasePath(storeId, type, theme);
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);
            var regexp = pattern == null ? null : new Regex(Regex.Escape(pattern), RegexOptions.None, TimeSpan.FromSeconds(1));
            var files = (await storageProvider.SearchAsync(folder, keyword))
                .Results.Where(x => x.Type != "folder" && (regexp?.IsMatch(x.Name) ?? true));
            var fileInfoes = new Dictionary<string, string>();
            var jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            foreach (var file in files)
            {
                TryAddFileContent(file, type, storageProvider, fileInfoes, jsonSettings);
            }
            var result = $"{{{string.Join(", ", fileInfoes.Keys.Select(x => $"\"{x}\": {fileInfoes[x]}"))}}}";
            return Content(result);
        }

        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Update)]
        [Route("save")]
        public async Task<ActionResult> SaveTemplates(string storeId, string theme, [FromBody] SaveFilesModel value, [FromQuery] bool draft = false,
            [FromQuery] string catalogHash = null)
        {
            var request = new PageBuilderContentRequest { StoreId = storeId, Theme = theme };
            if (await CheckStoreAccessAsync(request) is { } forbidden)
            {
                return forbidden;
            }

            if (!TryParseFiles(value, out var files))
            {
                return BadRequest(new { error = "files must be a JSON list of files." });
            }

            var gitFlow = await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted);
            if (IsPublication(files, draft, gitFlow) && !await HasPermissionsAsync([ModuleConstants.Security.Permissions.Publish]))
            {
                return Forbid();
            }

            var themeFiles = files.Where(IsThemeFile).ToList();
            if (!await HasPermissionsAsync(themeFiles.Select(x => contentPermissionService.GetThemeFileEditPermission(x.Path))))
            {
                return Forbid();
            }

            request.Files = files.ConvertAll(ToContentFile);
            if (await ValidateAsync(request) is { } invalid)
            {
                return invalid;
            }

            if (themeFiles.Count > 0 && await ValidateThemeFilesAsync(storeId, themeFiles, draft, catalogHash) is { } refusedThemeFiles)
            {
                return refusedThemeFiles;
            }

            if (!gitFlow || !draft)
            {
                await SaveFilesTo(storeId, theme, files, draft);
                return Ok();
            }

            return await SaveDraftToGitAsync(storeId, theme, files);
        }

        // a live write of a page is a publication; on the git flow, every live write is one
        private static bool IsPublication(List<SaveFileModel> files, bool draft, bool gitFlow) =>
            !draft && (gitFlow || files.Exists(file => file != null && GitPageLocation.IsPageContent(file.Type)));

        private async Task<bool> HasPermissionsAsync(IEnumerable<string> permissions) =>
            await contentPermissionService.HasPermissionsAsync(User, permissions);

        private async Task<ActionResult> ValidateThemeFilesAsync(string storeId, List<SaveFileModel> files, bool draft, string catalogHash)
        {
            var contents = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                contents[file.Path] = AsToken(file.Content);
            }

            try
            {
                await designerContentService.ValidateThemeFilesAsync(storeId, contents, draft, catalogHash, HttpContext.RequestAborted);
                return null;
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ThemeValidationException ex)
            {
                return BadRequest(new { error = ex.Message, errors = ex.Errors });
            }
        }

        private static bool IsThemeFile(SaveFileModel file) => string.Equals(file?.Type, Themes, StringComparison.OrdinalIgnoreCase);

        private static PageBuilderContentFile ToContentFile(SaveFileModel file) =>
            file == null ? null : new PageBuilderContentFile { ContentType = file.Type, Path = file.Path, Content = AsToken(file.Content) };

        private static bool TryParseFiles(SaveFilesModel value, out List<SaveFileModel> files)
        {
            files = [];
            if (value?.Files == null)
            {
                return false;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<List<SaveFileModel>>(value.Files);
                files = parsed ?? [];
                return parsed != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private async Task<ActionResult> SaveDraftToGitAsync(string storeId, string theme, List<SaveFileModel> files)
        {
            // A draft is a commit on a work branch, not a .page-draft blob. The blob was a single slot
            // per page, so two editors of the same page overwrote each other's draft and preview; their
            // branches do not.
            var pages = files.Where(x => GitPageLocation.IsPageContent(x.Type)).ToList();

            var saved = new List<GitPageCommit>();
            if (pages.Count > 0)
            {
                var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
                foreach (var page in pages)
                {
                    saved.Add(await filePageDraftService.CommitDraftAsync(storeId, repository, ToContentFile(page), SaveAction, User, HttpContext.RequestAborted));
                }
            }

            // Themes, schemas and everything else still live in blob storage — only pages and blogs moved
            // to git.
            var others = files.Except(pages).ToList();
            if (others.Count > 0)
            {
                await SaveFilesTo(storeId, theme, others, draft: true);
            }

            return Ok(new { pages = saved });
        }

        /// <summary>A theme file as the designer edits it: its draft, else what is live, with the version its save sends back</summary>
        [HttpGet]
        [Route("designer/theme-file")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ThemeFileContent>> GetDesignerThemeFile([FromQuery] string storeId, [FromQuery] string path)
        {
            var request = new PageBuilderContentRequest { StoreId = storeId, Files = [new PageBuilderContentFile { ContentType = Themes, Path = path }] };
            if (await CheckRequestAsync(request) is { } refused)
            {
                return refused;
            }

            return Ok(await themeDraftService.ReadAsync(storeId, path, HttpContext.RequestAborted));
        }

        /// <summary>Save designer changes as drafts</summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Update)]
        [Route("designer/save")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
        public async Task<ActionResult<DesignerSaveResult>> SaveDesignerChanges([FromQuery] string storeId, [FromBody] DesignerSaveRequest value,
            [FromQuery] string catalogHash = null)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var page = value?.Page;
            var themeFiles = value?.ThemeFiles ?? [];
            var request = new PageBuilderContentRequest { StoreId = storeId };
            if ((await CheckStoreAccessAsync(request) ?? await CheckThemeFilesAccessAsync(themeFiles.Select(x => x?.Path))) is { } forbidden)
            {
                return forbidden;
            }

            if (await designerSaveValidator.ValidateRequestAsync(value ?? new DesignerSaveRequest(), cancellationToken) is { } invalidRequest)
            {
                return invalidRequest;
            }

            var group = string.IsNullOrEmpty(page?.GroupId) ? null : await groupedPageService.GetByIdAsync(page.GroupId);
            if (!string.IsNullOrEmpty(page?.GroupId) && await CheckDesignerGroupAccessAsync(storeId, group) is { } refusedGroup)
            {
                return refusedGroup;
            }

            if (!TryParseDesignerFiles(page, themeFiles, out var files, out var parseError))
            {
                return BadRequest(new { error = parseError });
            }

            request.Files = files.ConvertAll(ToContentFile);
            if (await ValidateAsync(request) is { } invalid)
            {
                return invalid;
            }

            if (group != null && await CheckDesignerPageAsync(storeId, page, cancellationToken) is { } refusedPage)
            {
                return refusedPage;
            }

            if (themeFiles.Any(x => string.IsNullOrWhiteSpace(x.ETag)))
            {
                return StatusCode(StatusCodes.Status428PreconditionRequired, "Read each theme file with designer/theme-file and send its ETag before saving.");
            }

            var themeSaveFiles = files.Where(IsThemeFile).ToList();
            if (themeSaveFiles.Count > 0 && string.IsNullOrEmpty(catalogHash))
            {
                return BadRequest(new { error = CatalogRequiredError });
            }

            if (themeSaveFiles.Count > 0 && await ValidateThemeFilesAsync(storeId, themeSaveFiles, draft: true, catalogHash) is { } refusedThemeFiles)
            {
                return refusedThemeFiles;
            }

            var command = AbstractTypeFactory<DesignerSaveCommand>.TryCreateInstance();
            command.StoreId = storeId;
            command.SaveId = Request.GetOrCreateSaveId();
            command.Group = group;
            command.PageContent = page?.Content;
            command.PageETag = page?.ETag?.Trim();
            command.FilePage = files.Where(x => !IsThemeFile(x)).Select(ToContentFile).FirstOrDefault();
            command.ThemeFiles = themeFiles;
            try
            {
                return Ok(await designerContentService.SaveAsync(command, User, cancellationToken));
            }
            catch (PageBuilderContentConflictException ex)
            {
                return StatusCode(StatusCodes.Status412PreconditionFailed, ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Discard designer changes</summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Update)]
        [Route("designer/discard")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ContentSaveResult>> DiscardDesignerChanges([FromQuery] string storeId, [FromBody] DesignerDiscardRequest value)
        {
            var page = value?.Page;
            var themePaths = value?.ThemeFiles ?? [];
            var request = new PageBuilderContentRequest { StoreId = storeId };
            if ((await CheckStoreAccessAsync(request) ?? await CheckThemeFilesAccessAsync(themePaths)) is { } forbidden)
            {
                return forbidden;
            }

            if (await designerDiscardValidator.ValidateRequestAsync(value ?? new DesignerDiscardRequest(), HttpContext.RequestAborted) is { } invalidRequest)
            {
                return invalidRequest;
            }

            var group = string.IsNullOrEmpty(page?.GroupId) ? null : await groupedPageService.GetByIdAsync(page.GroupId);
            if (!string.IsNullOrEmpty(page?.GroupId) && await CheckDesignerGroupAccessAsync(storeId, group) is { } refusedGroup)
            {
                return refusedGroup;
            }

            request.Files = themePaths.Select(x => new PageBuilderContentFile { ContentType = Themes, Path = x }).ToList();
            if (!string.IsNullOrEmpty(page?.Path))
            {
                request.ContentType = page.Type.ToLowerInvariant();
                request.Pages = [page.Path];
            }

            if (await ValidateAsync(request) is { } invalid)
            {
                return invalid;
            }

            var command = AbstractTypeFactory<DesignerDiscardCommand>.TryCreateInstance();
            command.StoreId = storeId;
            command.SaveId = Request.GetOrCreateSaveId();
            command.Group = group;
            command.FilePage = string.IsNullOrEmpty(page?.Path) ? null : new PageBuilderContentFile { ContentType = request.ContentType, Path = page.Path };
            command.ThemeFiles = themePaths;
            try
            {
                return Ok(await designerContentService.DiscardAsync(command, User, HttpContext.RequestAborted));
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Reads the JSON of the file page and the theme files as they are, dates included.</summary>
        private static bool TryParseDesignerFiles(DesignerSavePage page, IList<DesignerSaveFile> themeFiles, out List<SaveFileModel> files, out string error)
        {
            files = [];
            error = null;
            var named = themeFiles.Select(x => (x.Path, Type: Themes, x.Content)).ToList();
            if (!string.IsNullOrEmpty(page?.Path))
            {
                named.Insert(0, (page.Path, page.Type.ToLowerInvariant(), page.Content));
            }

            foreach (var (path, type, content) in named)
            {
                if (!TryParseJson(content, out var token))
                {
                    error = $"\"{path}\" is not a JSON document.";
                    return false;
                }

                files.Add(new SaveFileModel { Path = path, Type = type, Content = token });
            }

            return true;
        }

        private static bool TryParseJson(string content, out JToken token)
        {
            token = null;
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            try
            {
                using var reader = new JsonTextReader(new StringReader(content)) { DateParseHandling = DateParseHandling.None };
                var parsed = JToken.ReadFrom(reader);
                // a theme file is saved as sent, so nothing may follow the document that was checked
                if (reader.Read())
                {
                    return false;
                }

                token = parsed;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private async Task<ActionResult> CheckThemeFilesAccessAsync(IEnumerable<string> themePaths)
        {
            var permissions = themePaths.Select(contentPermissionService.GetThemeFileEditPermission).ToList();

            return await HasPermissionsAsync(permissions) ? null : Forbid();
        }

        private async Task<ActionResult> CheckDesignerGroupAccessAsync(string storeId, GroupedPageBuilderPage group)
        {
            if (group == null)
            {
                return NotFound();
            }

            return await contentPermissionService.CanAccessPageAsync(User, group, storeId) ? null : Forbid();
        }

        /// <summary>Checks a database page as its own save does: its version and its shared components.</summary>
        private async Task<ActionResult> CheckDesignerPageAsync(string storeId, DesignerSavePage page, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(page.ETag))
            {
                return StatusCode(StatusCodes.Status428PreconditionRequired, "Read the page content and send its ETag before saving.");
            }

            return (await sharedComponentPlacementValidator.ValidateAsync(User, storeId, page.Content, cancellationToken)).ToActionResult();
        }

        /// <summary>
        /// Merge the editor's branch of the page into the base branch, and write the page to live storage of a repository
        /// the platform manages. The editor's own draft is what ships — never the bytes the caller happens to send.
        /// </summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Publish)]
        [Route("git/publish")]
        public async Task<ActionResult> GitPublish(string storeId, string path, string type, bool rebase = false)
        {
            if (await CheckRequestAsync(PageRequest(storeId, type, path)) is { } refused)
            {
                return refused;
            }

            var storeRepository = await gitContentPolicy.GetRepositoryInfoAsync(storeId, HttpContext.RequestAborted);
            if (storeRepository == null)
            {
                // the switch is off for this store: publish means what it always meant
                await publishingService.PublishingAsync(type ?? PagesContentType, storeId, path, publish: true);
                return Ok(new { state = nameof(GitPublishState.Merged) });
            }

            var location = GitLocation(storeRepository.Name, type, path);

            var draft = await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, location.Branch, HttpContext.RequestAborted);
            if (draft == null)
            {
                // nothing to merge; re-sync the live page anyway (repairs a failed or late live write)
                if (storeRepository.IsPlatformManaged)
                {
                    await gitLivePageService.SyncLivePageAsync(storeId, type ?? PagesContentType, path, location, removeWhenGone: false, HttpContext.RequestAborted);
                }

                return Ok(new { state = nameof(GitPublishState.AlreadyPublished) });
            }

            // The CI gate runs the full, schema-aware validator; this one only refuses to open a pull
            // request that could never pass it.
            var errors = PageEnvelopeValidator.Validate(ParsePage(draft));
            if (errors.Count > 0)
            {
                return BadRequest(new { errors });
            }

            if (rebase)
            {
                await RebaseDraftAsync(location, draft, path, storeId);
            }

            var result = await gitContentPublisher.MergeBranchAsync(location.Repository, location.Branch, $"publish {path} (store: {storeId})", HttpContext.RequestAborted);

            var response = await RespondToPublishAsync(result, location, path, canRebase: true);
            if (storeRepository.IsPlatformManaged && result.State is GitPublishState.Merged or GitPublishState.AlreadyPublished)
            {
                await gitLivePageService.SyncLivePageAsync(storeId, type ?? PagesContentType, path, location, removeWhenGone: false, HttpContext.RequestAborted);
            }

            return response;
        }

        /// <summary>
        /// The way out of a <see cref="GitPublishState.Conflict"/>: the draft, as a state, on top of today's
        /// base branch.
        /// <para>
        /// A page is one JSON document, so when the base branch has changed it since this branch was cut,
        /// git can rarely lay one edit over the other — and the work branch keeps its old merge base, so
        /// every retry reproduces the same conflict. Restoring a version does not help either: it commits
        /// onto the same branch. Before this, the only way out was someone closing the pull request and
        /// deleting the branch by hand.
        /// </para>
        /// <para>
        /// So the branch is moved to the base branch and the draft's bytes become one commit on it — the
        /// same idea promotion is built on: what is wanted is the file's state, not its history. The other
        /// edits are replaced by this one; they stay in the base branch's history, and the old branch's
        /// commits stay reachable by sha. The open pull request is kept and recomputed against the moved
        /// branch rather than closed and opened again. Only on request — the editor was shown what they
        /// are replacing — never as a default: silently taking either side would throw away someone's work.
        /// </para>
        /// <para>
        /// Moved, not deleted and cut again. Delete-then-create leaves a moment with no branch at all, and
        /// a failure in that moment leaves the draft as a commit nothing points at — while the next publish
        /// reads no draft and reports the page as already published, which is the one answer this flow must
        /// never give wrongly. It also cannot be done reliably: <see cref="IGitContentRepository.CreateBranchAsync"/>
        /// treats an existing ref as success without moving it, and GitHub can still report the deleted ref
        /// for a moment, so the branch would keep its old merge base and meet the same conflict again.
        /// </para>
        /// </summary>
        private async Task RebaseDraftAsync(GitPageFileLocation location, string draft, string path, string storeId)
        {
            var baseBranch = await GetBaseBranchAsync();
            var cancellationToken = HttpContext.RequestAborted;

            var baseSha = await gitContentRepository.GetBranchHeadShaAsync(location.Repository, baseBranch, cancellationToken)
                ?? throw new InvalidOperationException($"Could not resolve \"{baseBranch}\" to a commit.");

            // Where the draft is now, so it can be put back if what follows does not finish.
            var draftHead = await gitContentRepository.GetBranchHeadShaAsync(location.Repository, location.Branch, cancellationToken);

            try
            {
                await gitContentRepository.SetBranchAsync(location.Repository, location.Branch, baseSha, location.RepoPath, cancellationToken);

                var author = CurrentAuthor();
                var message = CommitMessage($"designer: publish {path} on top of {baseBranch}, replacing what changed there since this draft began (store: {storeId}, by: {author.Name})");
                await gitContentRepository.CommitFileAsync(location.Repository, location.RepoPath, draft, location.Branch, message, author, cancellationToken);
            }
            catch (Exception failure) when (draftHead != null)
            {
                try
                {
                    // Deliberately not the request's token. What is being undone here includes the request
                    // being cancelled — the editor closing the tab while the commit was in flight — and
                    // passing the token that has just been cancelled would mean the undo cannot run in
                    // exactly the case it exists for, leaving the branch on the base commit with the draft
                    // reachable only by a sha nobody is left to read. The call is bounded by the HTTP
                    // client's own timeout.
                    await gitContentRepository.SetBranchAsync(location.Repository, location.Branch, draftHead, location.RepoPath, CancellationToken.None);
                }
                catch (Exception restoreFailure)
                {
                    // Both the rebuild and the undo failed. The draft is still a commit in the repository
                    // and nothing but its sha can find it now, so the sha goes in the message.
                    throw new InvalidOperationException(
                        $"Publishing \"{path}\" on top of {baseBranch} failed, and moving the draft branch back failed too. " +
                        $"The draft is commit {draftHead}; it can be restored with: git branch {location.Branch} {draftHead}",
                        new AggregateException(failure, restoreFailure));
                }

                throw;
            }
            finally
            {
                gitContentHistory.Invalidate(location.Repository, location.RepoPath);
            }
        }

        /// <summary>
        /// Promotes a page to production: the state of its file on <c>BaseBranch</c>, placed onto
        /// <c>ReleaseBranch</c> as its own commit.
        /// <para>
        /// Per page, and never a merge of one shared branch into the other. Merging would ship everything
        /// else the base branch happens to be holding, which is exactly what an editor promoting one page
        /// does not mean to do. Moving the file's STATE rather than replaying commits also means a page
        /// edited several times since the last promotion needs no history to line up: production ends up
        /// with what dev has now, which is the only thing ever wanted here.
        /// </para>
        /// </summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Promote)]
        [Route("git/promote")]
        public async Task<ActionResult> GitPromote(string storeId, string path, string type)
        {
            if (await CheckRequestAsync(PageRequest(storeId, type, path)) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                // without git there is no second branch to promote into
                return NotFound();
            }

            var options = gitContentOptions.Value;
            var baseBranch = await GetBaseBranchAsync();
            if (string.IsNullOrWhiteSpace(options.ReleaseBranch))
            {
                // A configuration answer, not a missing page: this installation has no production contour,
                // and saying so is more useful than a 404 that reads as "no such page".
                return BadRequest(new
                {
                    error = $"{GitContentOptions.SectionName}:{nameof(GitContentOptions.ReleaseBranch)} is not set: " +
                            "this installation has no production branch to promote into.",
                });
            }

            var location = GitLocation(repository, type, path);

            var onBase = await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, baseBranch, HttpContext.RequestAborted);
            if (onBase == null)
            {
                return BadRequest(new
                {
                    error = $"\"{path}\" is not on {baseBranch}. Publish it there first — production " +
                            "follows what has already been through the dev environment.",
                });
            }

            var onRelease = await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, options.ReleaseBranch, HttpContext.RequestAborted);
            if (onRelease != null && PageJson.AreSame(onBase, onRelease))
            {
                // production already holds this exact page; opening a pull request would only produce an
                // empty one, and the editor would be left waiting for a merge that means nothing
                return Ok(new { state = nameof(GitPublishState.AlreadyPublished) });
            }

            var branch = PromoteBranch(type, path);
            await PreparePromoteBranchAsync(location.Repository, branch, location.RepoPath, options.ReleaseBranch);

            // The bytes come from the base branch, never from the request: promotion ships what the dev
            // environment has been serving, not what a client happens to send along with the button press.
            await gitContentRepository.CommitFileAsync(
                location.Repository,
                location.RepoPath,
                onBase,
                branch,
                CommitMessage($"promote {path} to {options.ReleaseBranch} (store: {storeId})"),
                CurrentAuthor(),
                HttpContext.RequestAborted);

            var result = await gitContentPublisher.MergeBranchIntoAsync(
                location.Repository, branch, $"promote {path} (store: {storeId})", options.ReleaseBranch, HttpContext.RequestAborted);

            return await RespondToShipAsync(result, location.Repository, location.RepoPath, branch, options.ReleaseBranch, path);
        }

        /// <summary>
        /// Remove the page from the base branch, and from live storage of a repository the platform manages once the
        /// removal is merged: live storage follows the branch, so a revert stays a rollback.
        /// </summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Publish)]
        [Route("git/unpublish")]
        public async Task<ActionResult> GitUnpublish(string storeId, string path, string type)
        {
            if (await CheckRequestAsync(PageRequest(storeId, type, path)) is { } refused)
            {
                return refused;
            }

            var storeRepository = await gitContentPolicy.GetRepositoryInfoAsync(storeId, HttpContext.RequestAborted);
            if (storeRepository == null)
            {
                await publishingService.PublishingAsync(type ?? PagesContentType, storeId, path, publish: false);
                return Ok();
            }

            var baseBranch = await GetBaseBranchAsync();
            var location = GitLocation(storeRepository.Name, type, path);

            if (await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, baseBranch, HttpContext.RequestAborted) == null)
            {
                // retry of a merged unpublish whose live removal failed; a page the branch never had stays live
                if (storeRepository.IsPlatformManaged && await gitLivePageService.IsRemovedFromBaseBranchAsync(location, HttpContext.RequestAborted))
                {
                    await gitLivePageService.SyncLivePageAsync(storeId, type ?? PagesContentType, path, location, removeWhenGone: true, HttpContext.RequestAborted);
                    return Ok(new { state = nameof(GitPublishState.AlreadyPublished) });
                }

                return NotFound(new { templatePath = path });
            }

            var hadBranch = await gitContentRepository.GetBranchHeadShaAsync(location.Repository, location.Branch, HttpContext.RequestAborted) != null;
            if (!hadBranch)
            {
                await gitContentRepository.CreateBranchAsync(location.Repository, location.Branch, baseBranch, HttpContext.RequestAborted);
            }

            // A branch cut from the production branch a moment ago still holds the page. One that was
            // already there may not: an earlier unpublish whose merge is waiting to be asked for again
            // has committed the deletion, and all that is left of this operation is the merge. Deleting
            // a file that is already gone is an error rather than a no-op, so that retry would fail here
            // instead of finishing the unpublish it is retrying.
            if (!hadBranch || await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, location.Branch, HttpContext.RequestAborted) != null)
            {
                await gitContentRepository.DeleteFileAsync(location.Repository, location.RepoPath, location.Branch, CommitMessage($"unpublish {path} (store: {storeId})"), CurrentAuthor(), HttpContext.RequestAborted);
                gitContentHistory.Invalidate(location.Repository, location.RepoPath);
            }

            var result = await gitContentPublisher.MergeBranchAsync(location.Repository, location.Branch, $"unpublish {path} (store: {storeId})", HttpContext.RequestAborted);

            var response = await RespondToPublishAsync(result, location, path);
            if (result.State == GitPublishState.Merged && storeRepository.IsPlatformManaged)
            {
                await gitLivePageService.SyncLivePageAsync(storeId, type ?? PagesContentType, path, location, removeWhenGone: true, HttpContext.RequestAborted);
            }

            return response;
        }

        /// <summary>
        /// The shape the builder's toolbar expects, answered from git: <c>published</c> is "the page
        /// exists in the production branch", <c>hasChanges</c> is "this editor's branch says something
        /// different", <c>pending</c> is "a pull request for it is open", and <c>awaitingMerge</c> is
        /// "that pull request is not going to merge itself".
        /// <para>
        /// The answer also names the flow in effect, because a client has to behave differently under
        /// each — the admin blade saves to git and unpublishes through git on the git flow — and asking
        /// here costs it nothing. Both flows answer in the same shape, so a caller reads one contract.
        /// </para>
        /// </summary>
        [HttpGet]
        [Route("git/publish-status")]
        public async Task<ActionResult<PagePublishStatus>> GitPublishStatus(string storeId, string path, string type)
        {
            if (await CheckRequestAsync(PageRequest(storeId, type, PathsOf(path))) is { } refused)
            {
                return refused;
            }

            var storeRepository = await gitContentPolicy.GetRepositoryInfoAsync(storeId, HttpContext.RequestAborted);
            var gitFlow = storeRepository != null;

            // A page that does not exist yet — the blade asking about a page it is about to create — has
            // no status; what the caller is after in that case is the flow.
            if (path.IsNullOrEmpty())
            {
                return Ok(PublishStatus(published: false, hasChanges: false, pending: false, gitFlow));
            }

            if (!gitFlow)
            {
                var status = await publishingService.PublishStatusAsync(type ?? PagesContentType, storeId, path);
                return Ok(PublishStatus(status.Published, status.HasChanges, pending: false, gitFlow: false));
            }

            var baseBranch = await GetBaseBranchAsync();
            var location = GitLocation(storeRepository.Name, type, path);

            var published = await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, baseBranch, HttpContext.RequestAborted);
            var draft = await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, location.Branch, HttpContext.RequestAborted);
            var pending = await gitContentPublisher.GetOpenPullRequestAsync(location.Repository, location.Branch, HttpContext.RequestAborted);

            return Ok(PublishStatus(
                published: published != null,
                // a draft that says the same thing as production is not a change, whatever its history
                hasChanges: draft != null && !PageJson.AreSame(draft, published),
                pending: pending != null,
                gitFlow: true,
                // the pull request is open but nothing will merge it: publishing again is what finishes it
                awaitingMerge: pending is { AutoMerging: false },
                production: await ProductionStatusAsync(location.Repository, location.RepoPath, published, type, path),
                liveBehind: storeRepository.IsPlatformManaged &&
                            await gitLivePageService.IsLivePageBehindAsync(storeId, type ?? PagesContentType, path, location, published, HttpContext.RequestAborted)));
        }

        /// <summary>
        /// Where this page stands on the production branch, or <c>null</c> when the installation has none.
        /// <para>
        /// Reported separately from the fields above rather than folded into them: a page can be published
        /// and production still be serving last week's version of it, and that in-between state is the one
        /// that makes an editor say the site did not update. Nothing before this could express it.
        /// </para>
        /// </summary>
        private async Task<PageProductionStatus> ProductionStatusAsync(string repository, string repoPath, string onBase, string type, string path)
        {
            var options = gitContentOptions.Value;
            if (string.IsNullOrWhiteSpace(options.ReleaseBranch))
            {
                return null;
            }

            var onRelease = await gitContentRepository.ReadFileAsync(repository, repoPath, options.ReleaseBranch, HttpContext.RequestAborted);
            var pending = await gitContentPublisher.GetOpenPullRequestAsync(repository, PromoteBranch(type, path), HttpContext.RequestAborted);

            var production = AbstractTypeFactory<PageProductionStatus>.TryCreateInstance();
            production.Published = onRelease != null;
            // Behind only means something once the page is on the base branch at all: a page that has
            // never been published is not "missing from production", it simply has not started.
            production.Behind = onBase != null && !PageJson.AreSame(onBase, onRelease);
            production.Pending = pending != null;
            production.AwaitingMerge = pending is { AutoMerging: false };
            return production;
        }

        private static PagePublishStatus PublishStatus(bool published, bool hasChanges, bool pending, bool gitFlow,
            bool awaitingMerge = false, PageProductionStatus production = null, bool liveBehind = false)
        {
            var status = AbstractTypeFactory<PagePublishStatus>.TryCreateInstance();
            status.Published = published;
            status.HasChanges = hasChanges;
            status.Pending = pending;
            // Told apart from pending on purpose: both mean "a pull request is open", but only this one
            // means the merge is not going to happen unless somebody asks for it again.
            status.AwaitingMerge = awaitingMerge;
            status.Flow = gitFlow ? GitFlow : BlobFlow;
            status.Production = production;
            status.LiveBehind = liveBehind;
            return status;
        }

        /// <summary>
        /// The versions of a page: commits on the production branch, and commits on any other branch that
        /// the production branch does not have yet.
        /// <para>
        /// Versions are found by the page's file history rather than by branch name, which is what makes an
        /// edit done outside the builder visible here at all: the module names its own work branches
        /// <c>designer/{user}/{slug}</c>, an edit made through Claude lives on a <c>content/*</c> branch, and
        /// neither has to know about the other's namespace for the commit to show up in this list.
        /// </para>
        /// <para>
        /// Reading history needs no permission beyond being signed in, as with every other read here — but
        /// note it does surface other editors' unpublished drafts, which blob storage never had.
        /// </para>
        /// </summary>
        [HttpGet]
        [Route("git/history")]
        public async Task<ActionResult> GitHistory(string storeId, string path, string type, int? take = null, string after = null)
        {
            var contentType = type.IsNullOrEmpty() ? PagesContentType : type;
            var request = PageRequest(storeId, contentType, PathsOf(path));
            request.HistoryTake = take;
            request.HistoryAfter = after;
            if (await CheckRequestAsync(request) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                // there is no history to show: in the blob flow a draft overwrites the page and keeps no versions
                return NotFound();
            }

            if (path.IsNullOrEmpty())
            {
                // a page that does not exist yet — the blade asking about one it is about to create
                return Ok(PageHistoryModel.Empty);
            }

            var location = GitLocation(repository, contentType, path);

            var history = await gitContentHistory.GetHistoryAsync(location.Repository, location.RepoPath, new GitHistoryQuery
            {
                BaseBranch = await GetBaseBranchAsync(),
                PublishedDepth = take,
                After = after,
            }, HttpContext.RequestAborted);

            // "mine" is the server's contribution: the repository knows which branches hold a commit, only
            // this end knows which of those branches is the caller's.
            return Ok(PageHistoryModel.From(history, location.Branch));
        }

        /// <summary>
        /// Continues editing from an earlier version: the content of <paramref name="sha"/> becomes a new
        /// commit on this editor's own work branch.
        /// <para>
        /// Forward only, and that is the whole design. Writing into the branch the version came from would
        /// mean force-pushing over whatever came after it — destroying the audit trail the version list
        /// exists to provide — or writing into a branch that belongs to another editor, which is the single
        /// global draft slot this flow removed. Appending instead keeps history append-only, leaves branch
        /// ownership at one editor per page, and gives a rollback that needs no git: restore, then publish.
        /// </para>
        /// </summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Update)]
        [Route("git/restore-version")]
        public async Task<ActionResult> GitRestoreVersion(string storeId, string path, string type, [BindRequired] string sha)
        {
            var contentType = type.IsNullOrEmpty() ? PagesContentType : type;
            var request = PageRequest(storeId, contentType, path);
            // a version is a commit; a branch name would mean "whatever is there when this runs"
            request.Ref = sha;
            if (await CheckRequestAsync(request) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                // without git there are no versions to continue from
                return NotFound();
            }

            var baseBranch = await GetBaseBranchAsync();
            var location = GitLocation(repository, contentType, path);

            var content = await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, sha, HttpContext.RequestAborted);
            if (content == null)
            {
                // no such commit, or the page did not exist in it
                return NotFound(new { templatePath = path, gitRef = sha });
            }

            // The same gate a save passes: restoring a version that CI would refuse to deploy would leave
            // the editor with a branch that cannot be published.
            var page = ParsePage(content);
            var errors = PageEnvelopeValidator.Validate(page);
            if (errors.Count > 0)
            {
                return BadRequest(new { errors });
            }

            if (await gitContentRepository.GetBranchHeadShaAsync(location.Repository, location.Branch, HttpContext.RequestAborted) == null)
            {
                await gitContentRepository.CreateBranchAsync(location.Repository, location.Branch, baseBranch, HttpContext.RequestAborted);
            }

            var author = CurrentAuthor();
            var shortSha = sha[..7];
            var message = CommitMessage($"designer: restore {path} from {shortSha} (store: {storeId}, by: {author.Name})");

            // Canonical bytes, not the ones read from the commit: an older version may have been written
            // before the canonical form existed, and re-committing it verbatim would make publish-status
            // report changes that are only line endings.
            var commitSha = await gitContentRepository.CommitFileAsync(location.Repository, location.RepoPath, PageJson.Serialize(page), location.Branch, message, author, HttpContext.RequestAborted);
            gitContentHistory.Invalidate(location.Repository, location.RepoPath);

            return Ok(new { branch = location.Branch, commitSha, restoredFrom = sha });
        }

        /// <summary>
        /// Redirects to the storefront's preview of this page at an exact commit: the editor's draft if
        /// they have one, otherwise what is published. A commit sha is immutable, so the link keeps
        /// showing what it showed when it was made.
        /// </summary>
        [HttpGet]
        [Route("git/preview")]
        public async Task<ActionResult> GitPreview(string storeId, string path, string type, [FromQuery(Name = "ref")] string requestedRef = null)
        {
            var request = PageRequest(storeId, type, path);
            // The link goes straight to the storefront: a branch name in it would stop meaning what the link said.
            request.Ref = requestedRef;
            if (await CheckRequestAsync(request) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                return NotFound();
            }

            var location = GitLocation(repository, type, path);

            var gitRef = requestedRef ?? await DraftOrPublishedHeadAsync(location.Repository, location.Branch);
            if (gitRef == null)
            {
                return NotFound(new { templatePath = path });
            }

            var storeUrl = await GetStorefrontUrlAsync(storeId);
            if (string.IsNullOrEmpty(storeUrl))
            {
                return BadRequest(new
                {
                    error = $"Nowhere to preview \"{path}\": store \"{storeId}\" has no storefront url. " +
                            $"Fill in the store's Url (or Secure URL), or set {ModuleConstants.Settings.General.StoreUrl.Name} to override it."
                });
            }

            var previewPath = await GetSettingAsync(ModuleConstants.Settings.General.StorePreviewPath) ?? DefaultPreviewPath;
            var pageId = EncodePageId(storeId, type, path);

            return Redirect($"{storeUrl.TrimEnd('/')}{previewPath}?pageId={Uri.EscapeDataString(pageId)}&ref={Uri.EscapeDataString(gitRef)}");
        }

        private async Task<string> DraftOrPublishedHeadAsync(string repository, string workBranch) =>
            await gitContentRepository.GetBranchHeadShaAsync(repository, workBranch, HttpContext.RequestAborted)
            ?? await gitContentRepository.GetBranchHeadShaAsync(repository, await GetBaseBranchAsync(), HttpContext.RequestAborted);

        /// <summary>
        /// Discard the blob draft of a page or theme file: of a page of a store without git, or of a theme file such as a
        /// section group. Refused when nothing is published, as the draft is then the only copy.
        /// </summary>
        [HttpPost]
        [Route("discard-draft")]
        [Authorize(ModuleConstants.Security.Permissions.Update)]
        public async Task<ActionResult> DiscardDraft(string storeId, string path, string type, string theme = null)
        {
            var contentType = string.IsNullOrEmpty(type) ? PagesContentType : type.ToLowerInvariant();
            if (contentType == Themes && !await HasPermissionsAsync([contentPermissionService.GetThemeFileEditPermission(path)]))
            {
                return Forbid();
            }

            if (await CheckRequestAsync(storeId, contentType, theme, [path]) is { } refused)
            {
                return refused;
            }

            if (GitPageLocation.IsPageContent(contentType) && await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                return BadRequest("The store's pages are in git: their drafts are discarded with git/discard-draft.");
            }

            if (contentType != Themes)
            {
                return await filePageDraftService.DiscardBlobDraftAsync(storeId, contentType, path, theme, HttpContext.RequestAborted) == DraftDiscardStatus.OnlyCopy
                    ? Conflict(OnlyCopyError)
                    : NoContent();
            }

            var draft = (await themeDraftService.DiscardDraftsAsync(storeId, [path], _ => Task.CompletedTask, HttpContext.RequestAborted)).Single();
            if (draft.Status == DraftDiscardStatus.OnlyCopy)
            {
                return Conflict(OnlyCopyError);
            }

            if (draft.Status == DraftDiscardStatus.Discarded)
            {
                var record = ContentHistoryRecord.Create(ModuleConstants.GitHistory.DiscardedSummary, Request.GetSaveId(), User);
                record.AddEntry(HistoryItem.ForThemeFile(path), draft.PublishedContent);
                await contentHistory.RecordAsync(storeId, record, HttpContext.RequestAborted);
            }

            return NoContent();
        }

        /// <summary>
        /// Throws this editor's draft of the page away, so the next read starts from what is published.
        /// The way out of a conflict, and the only one that does not quietly pick a winner.
        /// </summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Update)]
        [Route("git/discard-draft")]
        public async Task<ActionResult> GitDiscardDraft(string storeId, string path, string type)
        {
            if (await CheckRequestAsync(PageRequest(storeId, type, path)) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                return NotFound();
            }

            await filePageDraftService.DiscardGitDraftAsync(repository, type, path, User, HttpContext.RequestAborted);

            return Ok();
        }

        /// <summary>
        /// The blob draft files this store carried over from the flow that came before git, and whether
        /// each one still holds anything the repository does not.
        /// <para>
        /// Opting a store into the git flow leaves its "<c>foo.page-draft</c>" blobs exactly where they
        /// were, and two very different things end up wearing that one name: dead weight, for a page that
        /// has since been saved from the designer and now lives in git — and the only copy of somebody's
        /// unpublished work, for a page that has not, because <see cref="GetTemplate"/> still serves those
        /// from blob. Telling the two apart is all this endpoint does; deleting is a separate call, over
        /// the paths the caller picked out of this answer.
        /// </para>
        /// <para>
        /// Scanning <c>pages</c> covers blogs too: blob storage keeps them in a subfolder of the pages
        /// root and the repository mirrors that layout, so a draft found under <c>blogs/</c> maps to the
        /// repository path it would have been committed to anyway.
        /// </para>
        /// </summary>
        [HttpGet]
        [Route("git/legacy-drafts")]
        public async Task<ActionResult> GetLegacyDrafts(string storeId, string type, string theme, int skip = 0, int take = 50)
        {
            if (await CheckRequestAsync(storeId, type ?? PagesContentType, theme, []) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                // On the blob flow a "-draft" file IS the draft, not a leftover. There is nothing to clean
                // up here, and handing back a list would invite a caller to delete live content.
                return NotFound();
            }

            var contentType = type ?? PagesContentType;
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(
                GetContentBasePath(storeId, contentType, await GetCurrentThemeName(storeId, theme)));

            var drafts = await FindDraftBlobsAsync(storageProvider);

            // Walking folders is cheap; comparing each file against the repository is not. So page first
            // and only look up the window that was asked for.
            var window = drafts
                .OrderBy(x => x.RelativeUrl, StringComparer.OrdinalIgnoreCase)
                .Skip(Math.Max(skip, 0))
                .Take(Math.Clamp(take, 1, MaxLegacyDraftPageSize))
                .ToList();

            var items = new List<LegacyDraftInfo>();
            foreach (var draft in window)
            {
                items.Add(await DescribeLegacyDraftAsync(repository, storageProvider, contentType, draft));
            }

            return Ok(new { totalCount = drafts.Count, skip, take, items });
        }

        /// <summary>
        /// Deletes blob draft files left over from the pre-git flow — exactly the paths it is given, and
        /// nothing else.
        /// <para>
        /// Deliberately not <c>DELETE api/content/{type}/{store}</c>, which is the endpoint a caller
        /// reaches for first. That one resolves every url it is handed into both the draft and the
        /// published name and removes whichever exist, so asking it to delete "foo.page-draft" takes the
        /// live "foo.page" down with it.
        /// </para>
        /// </summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Delete)]
        [Route("git/legacy-drafts/delete")]
        public async Task<ActionResult> DeleteLegacyDrafts(string storeId, string type, string theme,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LegacyDraftsDeleteRequest request)
        {
            // An empty body is a dry run over nothing; a body that does not bind is refused by the request check.
            var contentType = type ?? PagesContentType;
            request ??= new LegacyDraftsDeleteRequest();
            var contentRequest = new PageBuilderContentRequest
            {
                StoreId = storeId,
                ContentType = contentType,
                Theme = theme,
                LegacyDrafts = request.Paths ?? [],
            };
            if (await CheckRequestAsync(contentRequest) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                return NotFound();
            }

            var paths = (request.Paths ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var storageProvider = blobContentStorageProviderFactory.CreateProvider(
                GetContentBasePath(storeId, contentType, await GetCurrentThemeName(storeId, theme)));

            var (deletable, skipped) = await SortLegacyDraftsAsync(repository, paths, storageProvider, contentType);

            if (!request.DryRun && deletable.Count > 0)
            {
                await storageProvider.RemoveAsync([.. deletable]);
            }

            return Ok(new { dryRun = request.DryRun, deleted = deletable, skipped });
        }

        /// <summary>
        /// Splits the given draft paths into the ones whose page has another copy — those are a cleanup —
        /// and the ones that are the page's whole copy, which are reported back untouched.
        /// </summary>
        private async Task<(List<string> Deletable, List<object> Skipped)> SortLegacyDraftsAsync(
            string repository, IEnumerable<string> paths, IBlobContentStorageProvider storageProvider, string contentType)
        {
            var deletable = new List<string>();
            var skipped = new List<object>();

            foreach (var path in paths)
            {
                var repoPath = GitLocation(repository, contentType, path).RepoPath;
                var inGit = await gitContentRepository.ReadFileAsync(repository, repoPath, await GetBaseBranchAsync(), HttpContext.RequestAborted);
                var published = inGit ?? await ReadPublishedBlobAsync(storageProvider, path);

                if (published != null && await storageProvider.GetBlobInfoAsync(path) != null &&
                    !IsSamePageDocument(await ReadBlobTextAsync(storageProvider, path), published))
                {
                    skipped.Add(new { path, reason = LegacyDraftHasChanges });
                }
                else if (published != null)
                {
                    deletable.Add(path);
                }
                else
                {
                    // Neither the repository nor a published blob holds this page, so this file is the
                    // whole of it: deleting it would not be a cleanup, it would be the loss of the page.
                    // Saving it once from the designer puts it in git and makes it deletable.
                    //
                    // A page that is merely absent from the repository is NOT this case — a store that
                    // opted in keeps pages it never re-saved, and there the published blob still serves
                    // the page after the draft goes.
                    skipped.Add(new { path, reason = LegacyDraftOnlyCopy });
                }
            }

            return (deletable, skipped);
        }

        /// <summary>
        /// Duplicates a page inside the content repository: a commit of the same document under a new
        /// name, on the copying editor's work branch.
        /// <para>
        /// It exists because the blob copy cannot be right here.
        /// <c>POST api/content/{type}/{store}/copy-file</c> always writes the duplicate as a
        /// "<c>-draft</c>" blob — the blob flow's way of saying "not published yet" — and on the git flow
        /// that produces a file no storefront serves, which no publish will ever pick up, and which the
        /// next deploy leaves behind. Unpublished here means "on a branch, not on the base branch", so
        /// that is what a copy has to be.
        /// </para>
        /// <para>
        /// A store not on the git flow gets <c>404</c>: the blob copy is correct for it, and the caller
        /// falls back to it rather than this endpoint reimplementing it.
        /// </para>
        /// </summary>
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Create)]
        [Route("git/copy")]
        public async Task<ActionResult> GitCopyPage(string storeId, string type, string srcPath, string destPath)
        {
            // A target the server picks needs no check of its own: it keeps the source's folder and extension.
            var contentType = type ?? PagesContentType;
            if (await CheckRequestAsync(PageRequest(storeId, contentType, [srcPath, .. PathsOf(destPath)])) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                return NotFound();
            }

            // What the editor sees, which is their own draft of the source when they have one. Copying
            // the published version instead would silently duplicate something other than what is on
            // screen.
            var source = await ReadPageFromGitAsync(repository, contentType, srcPath, draft: true, gitRef: null);
            if (source == null)
            {
                return BadRequest(new { error = $"\"{srcPath}\" is not in the content repository.", srcPath });
            }

            var target = destPath.IsNullOrEmpty() ? await NextFreeCopyPathAsync(repository, contentType, srcPath) : destPath;
            if (target == null)
            {
                return BadRequest(new { error = $"\"{srcPath}\" already has {MaxCopyIndex} copies; name the next one yourself.", srcPath });
            }

            var page = ParsePage(source);
            if (page.Type == JTokenType.Null)
            {
                return BadRequest(new { error = $"\"{srcPath}\" is not a page document.", srcPath });
            }

            var saved = await filePageDraftService.CommitDraftAsync(
                storeId,
                repository,
                new PageBuilderContentFile { Path = target, ContentType = contentType, Content = page },
                $"copy {WithoutDraftSuffix(srcPath)} to",
                User,
                HttpContext.RequestAborted);

            return Ok(saved);
        }

        /// <summary>
        /// The name a duplicate gets: "foo.page" becomes "foo_1.page", at the first index the repository
        /// does not already hold — or <c>null</c> when there is no free one within reach.
        /// <para>
        /// Deliberately the same shape the blob flow produces, so a store that switched flows goes on
        /// getting the names its editors are used to. The language segment is kept where the file has
        /// one ("foo.de.page" becomes "foo_1.de.page"), because it is part of which page this is.
        /// </para>
        /// </summary>
        private async Task<string> NextFreeCopyPathAsync(string repository, string contentType, string srcPath)
        {
            var path = WithoutDraftSuffix(srcPath);
            var extension = Path.GetExtension(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var name = path.GetFileNameWithoutLanguage();
            var language = path.GetLanguage();
            var languageSuffix = language.IsNullOrEmpty() ? string.Empty : $".{language}";
            var folder = path[..^(fileName.Length + extension.Length)];

            for (var index = 1; index <= MaxCopyIndex; index++)
            {
                var candidate = $"{folder}{name}_{index}{languageSuffix}{extension}";
                if (!await PageExistsInRepositoryAsync(repository, contentType, candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the repository holds this page anywhere that matters for naming: on the base branch,
        /// or on this editor's work branch. Leaving the branch out would hand the same name to two
        /// duplicates in a row, because a copy that has not been published lives only there.
        /// </summary>
        private async Task<bool> PageExistsInRepositoryAsync(string repository, string contentType, string path)
        {
            var location = GitLocation(repository, contentType, path);

            return await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, await GetBaseBranchAsync(), HttpContext.RequestAborted) != null
                || await gitContentRepository.ReadFileAsync(location.Repository, location.RepoPath, location.Branch, HttpContext.RequestAborted) != null;
        }

        /// <summary>The page's own name, whichever of the two names the caller happens to hold.</summary>
        private static string WithoutDraftSuffix(string path) =>
            path.EndsWith(DraftSuffix, StringComparison.OrdinalIgnoreCase) ? path[..^DraftSuffix.Length] : path;

        /// <summary>
        /// Whether one page still has a leftover blob draft from the flow that came before git, and
        /// whether that file is safe to throw away.
        /// <para>
        /// The per-page question, asked by the blade for the page it has open. The store-wide inventory
        /// answers the same thing for everything at once, which is a folder walk plus a repository read
        /// per file — far too much for opening one page.
        /// </para>
        /// </summary>
        [HttpGet]
        [Route("git/legacy-draft")]
        public async Task<ActionResult> GetLegacyDraft(string storeId, string type, string path, string theme)
        {
            if (await CheckRequestAsync(storeId, type ?? PagesContentType, theme, PathsOf(path)) is { } refused)
            {
                return refused;
            }

            var repository = await gitContentPolicy.GetRepositoryAsync(storeId, HttpContext.RequestAborted);
            if (repository == null)
            {
                return NotFound();
            }

            if (path.IsNullOrEmpty())
            {
                // a page being created has no path, and therefore nothing left over
                return Ok(new LegacyDraftInfo());
            }

            var contentType = type ?? PagesContentType;
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(
                GetContentBasePath(storeId, contentType, await GetCurrentThemeName(storeId, theme)));

            // The blade may hold either name for the page — the list strips the suffix for display but
            // keeps it on the url of a page that only ever had a draft.
            var blobInfo = await storageProvider.GetBlobInfoAsync(LegacyDraftPathOf(path));

            return Ok(blobInfo == null
                ? new LegacyDraftInfo()
                : await DescribeLegacyDraftAsync(repository, storageProvider, contentType, blobInfo));
        }

        /// <summary>The draft blob name of a page, whichever of the two names the caller already holds.</summary>
        private static string LegacyDraftPathOf(string path) =>
            path.EndsWith(DraftSuffix, StringComparison.OrdinalIgnoreCase) ? path : path + DraftSuffix;

        private async Task<LegacyDraftInfo> DescribeLegacyDraftAsync(string repository, IBlobContentStorageProvider storageProvider,
            string contentType, BlobEntry draft)
        {
            var repoPath = GitLocation(repository, contentType, draft.RelativeUrl).RepoPath;
            var inGit = await gitContentRepository.ReadFileAsync(repository, repoPath, await GetBaseBranchAsync(), HttpContext.RequestAborted);
            var inBlob = await ReadBlobTextAsync(storageProvider, draft.RelativeUrl);
            // The published blob matters as much as the repository here. A store that opted in keeps
            // every page it had, and one never saved from the designer since is in blob storage only:
            // for it, "foo.page" is the live page and this draft is an edit on top of it.
            var published = await ReadPublishedBlobAsync(storageProvider, draft.RelativeUrl);

            return new LegacyDraftInfo
            {
                Exists = true,
                BlobPath = draft.RelativeUrl,
                RepoPath = repoPath,
                ModifiedDate = draft.ModifiedDate,
                ExistsInGit = inGit != null,
                // Nothing else holds this page: deleting the draft would not tidy up after the page,
                // it would be the page going away.
                IsOnlyCopy = inGit == null && published == null,
                // Compared against whatever currently serves the page — the repository when it has it,
                // the published blob otherwise. Comparing against git alone called every blob-only
                // page's draft "changed", which is no answer at all for the stores being cleaned up.
                DiffersFromCurrent = !IsSamePageDocument(inBlob, inGit ?? published),
            };
        }

        /// <summary>
        /// Whether two page documents say the same thing, whatever their formatting.
        /// <para>
        /// Compared as documents rather than as bytes, unlike publish status. Legacy drafts were written
        /// with a four-space indent and CRLF line endings, so a byte comparison would report every single
        /// one of them as changed and the flag would carry no information. The question being asked here
        /// is only whether the draft still holds content the repository has not got.
        /// </para>
        /// <para>
        /// A draft that will not parse counts as different: it cannot be shown to be safe to delete, and
        /// "unknown" belongs on the cautious side of a decision that destroys the only copy.
        /// </para>
        /// </summary>
        private static bool IsSamePageDocument(string left, string right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            try
            {
                return JToken.DeepEquals(JToken.Parse(left), JToken.Parse(right));
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        /// <summary>
        /// The published page next to this draft, or <c>null</c> when there is none. Read rather than
        /// merely probed, because it is what the draft is compared against.
        /// </summary>
        private static async Task<string> ReadPublishedBlobAsync(IBlobContentStorageProvider storageProvider, string draftPath)
        {
            var published = WithoutDraftSuffix(draftPath);

            return await storageProvider.GetBlobInfoAsync(published) == null
                ? null
                : await ReadBlobTextAsync(storageProvider, published);
        }

        private static async Task<string> ReadBlobTextAsync(IBlobContentStorageProvider storageProvider, string relativeUrl)
        {
            await using var stream = await storageProvider.OpenReadAsync(relativeUrl);
            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Every "-draft" blob under the provider's root. The provider lists one folder at a time, so the
        /// recursion is ours; folders already seen are not re-entered, because a provider that reports a
        /// folder as its own child would otherwise spin here forever.
        /// </summary>
        private static async Task<IList<BlobEntry>> FindDraftBlobsAsync(IBlobContentStorageProvider storageProvider)
        {
            var found = new List<BlobEntry>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folders = new Queue<string>();
            folders.Enqueue(null); // the provider's root

            while (folders.Count > 0)
            {
                var entries = (await storageProvider.SearchAsync(folders.Dequeue(), null)).Results;

                foreach (var entry in entries)
                {
                    var isFolder = entry.Type.EqualsIgnoreCase(FolderEntryType);

                    if (isFolder && visited.Add(entry.RelativeUrl))
                    {
                        folders.Enqueue(entry.RelativeUrl);
                    }
                    else if (!isFolder && entry.Name.EndsWith(DraftSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add(entry);
                    }
                }
            }

            return found;
        }

        private static string[] PathsOf(params string[] paths) => paths.Where(path => !path.IsNullOrEmpty()).ToArray();

        private static PageBuilderContentRequest PageRequest(string storeId, string type, params string[] pages) =>
            new() { StoreId = storeId, ContentType = type ?? PagesContentType, Pages = pages };

        private Task<ActionResult> CheckRequestAsync(string storeId, string type, string theme, IList<string> paths = null) =>
            CheckRequestAsync(new PageBuilderContentRequest
            {
                StoreId = storeId,
                ContentType = type,
                Theme = theme,
                Paths = paths ?? [],
            });

        /// <summary>
        /// Store access first, so a user bound to another store cannot tell from a 400 which stores exist.
        /// The permission itself is on the action's [Authorize] attributes.
        /// </summary>
        private async Task<ActionResult> CheckRequestAsync(PageBuilderContentRequest request) =>
            await CheckStoreAccessAsync(request) ?? await ValidateAsync(request);

        private async Task<ActionResult> CheckStoreAccessAsync(PageBuilderContentRequest request)
        {
            var storeAccess = await authorizationService.AuthorizeAsync(User, request, new PageBuilderAuthorizationRequirement());
            return storeAccess.Succeeded ? null : Forbid();
        }

        private async Task<ActionResult> ValidateAsync(PageBuilderContentRequest request)
        {
            // What did not bind, a malformed body or a missing required value, is the caller's mistake too.
            var bindingErrors = ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "The request could not be read." : error.ErrorMessage)
                .ToList();
            if (bindingErrors.Count > 0)
            {
                return BadRequest(new { error = string.Join(" ", bindingErrors) });
            }

            return await requestValidator.ValidateRequestAsync(request, HttpContext.RequestAborted);
        }

        private async Task<ActionResult> RespondToPublishAsync(GitPublishResult result, GitPageFileLocation location, string path,
            bool canRebase = false) =>
            await RespondToShipAsync(result, location.Repository, location.RepoPath, location.Branch, await GetBaseBranchAsync(), path, canRebase);

        /// <summary>
        /// The tail every shipping operation shares — publish, unpublish and promote differ in which
        /// branch they merged into, and in nothing else once the merge has happened.
        /// <para>
        /// <paramref name="canRebase"/> is whether a conflict has a way out from the builder: publishing
        /// the draft again with <c>rebase=true</c> (see <see cref="RebaseDraftAsync"/>). Only a publish
        /// offers it; an unpublish or a promotion that conflicts is a state for a person to look at.
        /// </para>
        /// </summary>
        private async Task<ActionResult> RespondToShipAsync(GitPublishResult result, string repository, string repoPath, string branch,
            string mergedInto, string path, bool canRebase = false)
        {
            if (result.State == GitPublishState.Merged)
            {
                await gitContentRepository.DeleteBranchAsync(repository, branch, repoPath, HttpContext.RequestAborted);

                // the merge moved that branch, so what it said about this page a moment ago is the
                // pre-merge content — status would report the page as still unshipped
                gitContentRepository.InvalidateRead(repository, repoPath, mergedInto);
                // and the draft version that just shipped is now a published one
                gitContentHistory.Invalidate(repository, repoPath);
            }

            if (result.State == GitPublishState.Conflict)
            {
                var wayOut = canRebase
                    ? " Re-read the page and apply the change again, or publish this draft as it is on top of the current page — that replaces what changed there."
                    : " Re-read the page and apply the change again.";

                return Conflict(new
                {
                    error = $"\"{path}\" changed in {mergedInto} while this draft was being written, and the two edits cannot be merged.{wayOut}",
                    pullRequest = result.PullRequestNumber,
                    url = result.Url,
                    canRebase,
                });
            }

            return Ok(new { state = result.State.ToString(), pullRequest = result.PullRequestNumber, url = result.Url });
        }

        /// <summary>
        /// Makes the promote branch ready to commit this promotion to.
        /// <para>
        /// It is reused only while its pull request is still open — that is what makes two editors
        /// promoting the same page land on one pull request rather than two. Once that pull request is
        /// gone the branch has to be cut again, because it can outlive it: the merge is a squash, so the
        /// branch's head is not on the production branch's history afterwards, and GitHub deletes a
        /// merged head only where the repository is configured to. Committing onto the leftover would
        /// make the promotion a three-way merge of two full rewrites of the same file — a conflict every
        /// retry reproduces, since the stale merge base never moves.
        /// </para>
        /// </summary>
        private async Task PreparePromoteBranchAsync(string repository, string branch, string repoPath, string releaseBranch)
        {
            var leftover = await gitContentRepository.GetBranchHeadShaAsync(repository, branch, HttpContext.RequestAborted);
            if (leftover != null &&
                await gitContentPublisher.GetOpenPullRequestAsync(repository, branch, HttpContext.RequestAborted) != null)
            {
                return;
            }

            if (leftover != null)
            {
                await gitContentRepository.DeleteBranchAsync(repository, branch, repoPath, HttpContext.RequestAborted);
            }

            await gitContentRepository.CreateBranchAsync(repository, branch, releaseBranch, HttpContext.RequestAborted);
        }

        // Promotion is per page, not per editor: the branch carries no {user}, so two people promoting the
        // same page reuse one branch and one pull request instead of racing with two.
        private string PromoteBranch(string type, string path) =>
            GitPageLocation.BranchFor(gitContentOptions.Value.PromoteBranchTemplate, userName: null,
                GitPageLocation.ContentPath(type, path));

        /// <summary>
        /// Where this store's storefront lives, for a preview link.
        /// <para>
        /// The store itself is the source: it already records its storefront url, and it is the only answer
        /// that can differ per store. <see cref="ModuleConstants.Settings.General.StoreUrl"/> is a single
        /// value for the whole installation, so on a platform with more than one store it can only be right
        /// about one of them — it stays supported as a deliberate override (a staging host, a tunnel) and is
        /// consulted first for installations that already rely on it.
        /// </para>
        /// </summary>
        private async Task<string> GetStorefrontUrlAsync(string storeId)
        {
            var configured = await GetSettingAsync(ModuleConstants.Settings.General.StoreUrl);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.Trim();
            }

            if (string.IsNullOrEmpty(storeId))
            {
                return null;
            }

            var store = await storeService.GetNoCloneAsync(storeId);
            // https first: a preview link is opened in a browser next to the platform, which is served over
            // https itself, and a mixed-content redirect is a dead end
            var fromStore = FirstFilled(store?.SecureUrl, store?.Url);

            return fromStore?.Trim();
        }

        private static string FirstFilled(params string[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        private async Task<string> GetSettingAsync(SettingDescriptor descriptor)
        {
            var setting = await settingsManager.GetObjectSettingAsync(descriptor.Name);
            return setting?.Value?.ToString();
        }

        // base64("<storeId>::<contentType>::<relativeUrl>") with '=' replaced by '-', matching what the
        // builder puts in a preview link and what the storefront decodes (DesignerPreviewController).
        // The url stays relative to its content root — for a blog article that is the blogs folder, which
        // the storefront re-adds when it looks the file up.
        private static string EncodePageId(string storeId, string contentType, string path)
        {
            var raw = $"{storeId}::{(contentType.IsNullOrEmpty() ? PagesContentType : contentType)}::{path.Replace('\\', '/').TrimStart('/')}";
            return Convert.ToBase64String(PageJson.Encoding.GetBytes(raw)).Replace('=', '-');
        }

        private static JToken ParsePage(string json)
        {
            try
            {
                return JToken.Parse(json);
            }
            catch (JsonReaderException)
            {
                return JValue.CreateNull();
            }
        }

        /// <summary>
        /// A commit message with the authenticated login recorded under it. Git authorship is signed with
        /// a shared address, so this trailer is the only attribution that survives — and because the
        /// server writes it from the identity on the request, it is one a client cannot dress up.
        /// </summary>
        private string CommitMessage(string summary) => GitCommitMessage.WithVcUser(summary, User?.Identity?.Name);

        private GitCommitAuthor CurrentAuthor() => gitContentOptions.Value.GetCommitAuthor(User);

        private static JToken AsToken(object content)
        {
            return content as JToken ?? (content == null ? JValue.CreateNull() : JToken.FromObject(content));
        }

        private async Task SaveFilesTo(string storeId, string theme, IReadOnlyCollection<SaveFileModel> files, bool draft, CancellationToken cancellationToken = default)
        {
            var providers = new Dictionary<string, IBlobContentStorageProvider>();

            var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            var themeName = await GetCurrentThemeName(storeId, theme);

            var changedFiles = new Dictionary<string, List<GenericChangedEntry<FileEntity>>>();
            var themeFiles = new List<(string Path, string Content)>();

            // a theme publication deletes the drafts it moved live
            if (files.Any(x => string.Equals(x.Type, Themes, StringComparison.OrdinalIgnoreCase)))
            {
                await distributedLock.ExecuteAsync(ThemeStorage.GetLockResource(storeId), _ => WriteFilesAsync(), cancellationToken: cancellationToken);
            }
            else
            {
                await WriteFilesAsync();
            }

            foreach (var (type, entries) in changedFiles)
            {
                await eventPublisher.Publish(new PageBuilderContentChangedEvent(type, entries), cancellationToken);
            }

            if (themeFiles.Count > 0)
            {
                var record = ContentHistoryRecord.Create(draft ? ModuleConstants.GitHistory.SavedSummary : ModuleConstants.GitHistory.PublishedSummary, Request.GetSaveId(), User);
                foreach (var (path, content) in themeFiles)
                {
                    record.AddEntry(HistoryItem.ForThemeFile(path), content);
                }

                await contentHistory.RecordAsync(storeId, record, cancellationToken);
            }

            async Task WriteFilesAsync()
            {
                foreach (var file in files)
                {
                    var type = file.Type.ToLowerInvariant();
                    if (!providers.TryGetValue(type, out var storageProvider))
                    {
                        storageProvider = blobContentStorageProviderFactory.CreateProvider(GetContentBasePath(storeId, type, themeName));
                        providers[type] = storageProvider;
                    }
                    var content = file.Content;
                    var targetPath = publishingService.GetRelativeDraftUrl(file.Path, draft);
                    await using var targetStream = await storageProvider.OpenWriteAsync(targetPath);
                    await using var writer = new StreamWriter(targetStream);
                    var stringContent = JsonConvert.SerializeObject(content, settings);
                    await writer.WriteAsync(stringContent);
                    if (type == Themes)
                    {
                        themeFiles.Add((file.Path, stringContent));
                    }

                    if (!changedFiles.TryGetValue(type, out var entries))
                    {
                        entries = new List<GenericChangedEntry<FileEntity>>();
                        changedFiles.Add(type, entries);
                    }

                    entries.Add(CreateChangedEntry(file.Path, file.Type, EntryState.Modified));
                }
            }
        }

        private static GenericChangedEntry<FileEntity> CreateChangedEntry(string path, string type, EntryState state)
        {
            var file = AbstractTypeFactory<FileEntity>.TryCreateInstance();
            file.Path = path;
            file.Type = type;

            return new GenericChangedEntry<FileEntity>(file, state);
        }

        private async Task<string> GetSettingsFilesFromFolder(string storeId, string theme, string folder)
        {
            var themeName = await GetCurrentThemeName(storeId, theme);
            var templatesFolder = $"{themeName}/config/schemas/{folder}";
            var basePath = GetContentBasePath(storeId, Themes, themeName);
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);
            var allFiles = await storageProvider.SearchAsync(templatesFolder, null);
            var files = allFiles.Results.Where(x => x.Name.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase));
            var response = string.Join(", ", files.Select(file => $"\"{GetKey(null, file)}\": {GetContent(file, storageProvider)}"));
            var result = $"{{{response}}}";
            return result;
        }

        private async Task<JArray> GetSchemasCatalogForFolder(string storeId, string theme, string folder, bool filterInternal)
        {
            var themeName = await GetCurrentThemeName(storeId, theme);
            var schemasFolder = $"{themeName}/config/schemas/{folder}";
            var basePath = GetContentBasePath(storeId, Themes, themeName);
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);
            var allFiles = await storageProvider.SearchAsync(schemasFolder, null);
            var files = allFiles.Results.Where(x =>
                x.Type != "folder" &&
                x.Name.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase));

            var result = new JArray();
            foreach (var file in files)
            {
                var key = Path.GetFileNameWithoutExtension(file.Name);
                if (filterInternal && key.StartsWith('_'))
                {
                    continue;
                }

                try
                {
                    var raw = GetContent(file, storageProvider);
                    var json = JObject.Parse(raw);
                    // Static section schemas describe page-level settings panels — they are not
                    // selectable as content sections. Hide them from the catalog; their `settings[]`
                    // is merged into the template response by `MergeStaticSectionsIntoTemplate`.
                    if (folder == SchemaKindSections && IsStaticEntry(json))
                    {
                        continue;
                    }

                    var entry = new JObject { ["key"] = key };
                    CopySchemaMetadata(json, entry, folder);
                    result.Add(entry);
                }
                catch
                {
                    // Skip files that cannot be parsed.
                }
            }

            return result;
        }

        // Upper bound for the catalog's derived `description` summary. Full descriptions average
        // ~1150 chars across a real theme (141 sections ≈ 162 KB); the catalog only needs enough
        // to PICK an entry, so it carries a short head summary instead — see DeriveCatalogSummary.
        internal const int CatalogSummaryMaxLength = 200;

        internal static void CopySchemaMetadata(JObject source, JObject target, string kind)
        {
            // Catalog metadata is intentionally narrow — only fields the LLM uses while picking.
            // Designer-only hints (`displayField`, `icon`, `tab`, `sort`, `group*`) are stripped.
            // `includeShared` is stripped — it matters during field-list resolution in Phase B
            // and the agent reads it from the full schema response there.
            // `static` is stripped — static sections are entirely hidden from the agent (their
            // `settings[]` is merged into the template response instead).
            // `description` is exposed only for kinds where the agent picks an entry by intent
            // (regular sections, templates, blocks). For objects/shared descriptions add noise.
            // It is shortened to a head summary here; the full description is served by
            // GetSchemaByKey (Phase B) once the agent commits to an entry.
            var includeDescription = kind is SchemaKindSections or SchemaKindTemplates or SchemaKindBlocks;
            var properties = includeDescription
                ? new[] { "name", "description" }
                : new[] { "name" };

            foreach (var property in properties)
            {
                var token = source[property];
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }

                target[property] = property == "description" && token.Type == JTokenType.String
                    ? DeriveCatalogSummary(token.Value<string>())
                    : token.DeepClone();
            }
        }

        /// <summary>
        /// Reduces a full schema description to a short head summary for the catalog listing.
        /// Theme descriptions follow a "&lt;what it is&gt; Use when: … Skip when: … &lt;field notes&gt;"
        /// structure; the head before "Use when:" is the selection signal. The verbose remainder
        /// (guidance + per-field notes, which duplicate the schema's <c>settings[]</c>) is dropped
        /// from the catalog and served in full by GetSchemaByKey when an entry is actually used.
        /// </summary>
        internal static string DeriveCatalogSummary(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            var marker = description.IndexOf("Use when", StringComparison.OrdinalIgnoreCase);
            var head = (marker > 0 ? description[..marker] : description).Trim();
            if (head.Length == 0)
            {
                head = description.Trim();
            }

            if (head.Length <= CatalogSummaryMaxLength)
            {
                return head;
            }

            // Prefer cutting at the last sentence boundary within the cap; else hard-cut + ellipsis.
            var slice = head[..CatalogSummaryMaxLength];
            var lastStop = slice.LastIndexOfAny(['.', '!', '?']);
            return lastStop >= CatalogSummaryMaxLength / 2
                ? slice[..(lastStop + 1)]
                : slice.TrimEnd() + "…";
        }

        private void TryAddFileContent(BlobEntry file, string type, IBlobContentStorageProvider storageProvider, Dictionary<string, string> fileInfoes, JsonSerializerSettings jsonSettings)
        {
            try
            {
                var key = GetKey(type, file);
                if (fileInfoes.ContainsKey(key))
                {
                    return;
                }

                var pageContent = GetPageContent(file, storageProvider);
                if (pageContent != null)
                {
                    var content = JsonConvert.SerializeObject(pageContent, jsonSettings);
                    fileInfoes.Add(key, content);
                }
            }
            catch
            {
                // Skip files that cannot be read or parsed
            }
        }

        private static string GetKey(string type, BlobEntry entry)
        {
            return type == null
                ? Path.GetFileNameWithoutExtension(entry.Name)
                : $"{type}::{entry.RelativeUrl}";
        }

        private ContentModel GetPageContent(BlobEntry entry, IBlobContentStorageProvider provider)
        {
            try
            {
                using var reader = new StreamReader(provider.OpenRead(entry.RelativeUrl));
                var content = reader.ReadToEnd();
                dynamic json = JsonConvert.DeserializeObject(content);
                var result = new ContentModel
                {
                    Name = json.settings.name.ToString(),
                    PreviewUrl = json.settings.permalink.ToString(),
                    Path = entry.RelativeUrl
                };
                return result;
            }
            catch
            {
                return null;
            }
        }

        private string GetContent(BlobEntry entry, IBlobContentStorageProvider provider)
        {
            using var reader = new StreamReader(provider.OpenRead(entry.RelativeUrl));
            return reader.ReadToEnd();
        }

        private string GetContentBasePath(string storeId, string contentType, string theme)
        {
            var retVal = pathResolver.GetContentBasePath(contentType, storeId, theme);
            return retVal;
        }

        private async Task<string> GetCurrentThemeName(string storeId, string themeName)
        {
            if (!string.IsNullOrEmpty(themeName))
            {
                return themeName;
            }
            var store = await storeService.GetNoCloneAsync(storeId, StoreResponseGroup.DynamicProperties.ToString());
            return store?.DynamicProperties.FirstOrDefault(x => x.Name == ModuleConstants.Theme.ThemeNamePropertyName)?.Values?.FirstOrDefault()?.Value?.ToString() ?? DefaultTheme;
        }

        public class SaveFilesModel
        {
            public string Files { get; set; }
        }

        public class SaveFileModel
        {
            public string Path { get; set; }
            public string Type { get; set; }
            public object Content { get; set; }
        }
    }
}
