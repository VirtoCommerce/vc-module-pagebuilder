using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    [Route("api/pagebuilder")]
    [Authorize]
    public class PageBuilderController(IStoreService storeService,
            IContentPathResolver pathResolver,
            IBlobContentStorageProviderFactory blobContentStorageProviderFactory,
            IPublishingService publishingService,
            IEventPublisher eventPublisher,
            IOptions<GitContentOptions> gitContentOptions,
            IGitContentPolicy gitContentPolicy,
            IGitContentRepository gitContentRepository,
            IGitContentHistory gitContentHistory,
            IGitContentPublisher gitContentPublisher,
            ISettingsManager settingsManager,
            IAuthorizationService authorizationService
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
        private const string DefaultTheme = "default";
        private const string JsonContentType = "application/json";
        private const string JsonExtension = ".json";
        private const string SchemaKindSections = "sections";
        private const string SchemaKindTemplates = "templates";
        private const string SchemaKindBlocks = "blocks";
        private const string SchemaKindObjects = "objects";
        private const string SchemaKindShared = "shared";
        // The blob name of a draft in the flow that came before git: the page's name with this appended.
        // Only the legacy-draft cleanup deals in these; everywhere else GitPageLocation strips the suffix.
        private const string DraftSuffix = "-draft";
        private const string FolderEntryType = "folder";
        private const int MaxLegacyDraftPageSize = 200;
        private const string LegacyDraftOnlyCopy = "only-copy";
        // How many "_N" names a duplicate will try before asking the caller to pick one. A page with
        // this many copies is a naming problem, not a paging problem.
        private const int MaxCopyIndex = 100;


        [HttpGet]
        [Route("template")]
        public async Task<ActionResult> GetTemplate(string storeId, string theme, string path, string type, bool draft = false, [FromQuery(Name = "ref")] string gitRef = null)
        {
            // With the git flow on, a page is read from git: the caller's URL does not change, because
            // resolving "this editor's draft, else what is published" is the server's job.
            if (GitPageLocation.IsPageContent(type) && !path.IsNullOrEmpty() &&
                await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                var fromGit = await ReadPageFromGitAsync(type, path, draft, gitRef);
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

            var basePath = GetContentBasePath(storeId, type, theme);
            if (!path.IsNullOrEmpty())
            {
                var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);

                var filePath = publishingService.GetRelativeDraftUrl(path, draft);
                var blobInfo = await storageProvider.GetBlobInfoAsync(filePath);

                if (blobInfo != null)
                {
                    var stream = await storageProvider.OpenReadAsync(blobInfo.RelativeUrl);
                    return File(stream, MimeTypeResolver.ResolveContentType(blobInfo.Name));
                }

                if (draft)
                {
                    var originalFilePath = publishingService.GetRelativeDraftUrl(path, false);
                    var originalBlobInfo = await storageProvider.GetBlobInfoAsync(originalFilePath);

                    if (originalBlobInfo != null)
                    {
                        var stream = await storageProvider.OpenReadAsync(originalBlobInfo.RelativeUrl);
                        return File(stream, MimeTypeResolver.ResolveContentType(originalBlobInfo.Name));
                    }
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
        private async Task<string> ReadPageFromGitAsync(string type, string path, bool draft, string gitRef)
        {
            var location = GitLocation(type, path);

            if (!string.IsNullOrEmpty(gitRef))
            {
                // an exact commit — what a preview link points at
                return await gitContentRepository.ReadFileAsync(location.RepoPath, gitRef, HttpContext.RequestAborted);
            }

            if (draft)
            {
                var onBranch = await gitContentRepository.ReadFileAsync(location.RepoPath, location.Branch, HttpContext.RequestAborted);
                if (onBranch != null)
                {
                    return onBranch;
                }
            }

            return await gitContentRepository.ReadFileAsync(location.RepoPath, gitContentOptions.Value.BaseBranch, HttpContext.RequestAborted);
        }

        /// <summary>
        /// Where this page lives in the repository and on which branch this editor drafts it. Both are
        /// derived from the same content path, so a page and a blog article that happen to share a
        /// relative url stay two files on two branches.
        /// </summary>
        private (string RepoPath, string Branch) GitLocation(string type, string path)
        {
            var options = gitContentOptions.Value;
            var contentPath = GitPageLocation.ContentPath(type, path);

            return (
                GitPageLocation.RepoPath(options.PagesRoot, contentPath),
                GitPageLocation.BranchFor(options.BranchTemplate, User?.Identity?.Name, contentPath));
        }

        private ContentResult PageContent(string json) => Content(json, JsonContentType);

        [HttpGet]
        [Route("settings")]
        public async Task<ActionResult> GetSettings(string storeId, string theme)
        {
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
            var result = await GetSettingsFilesFromFolder(storeId, theme, "templates");
            return Content(result, JsonContentType);
        }

        [HttpGet]
        [Route("objects")]
        public async Task<ActionResult> GetObjects(string storeId, string theme)
        {
            var result = await GetSettingsFilesFromFolder(storeId, theme, SchemaKindObjects);
            return Ok(result);
        }

        [HttpGet]
        [Route("sections")]
        public async Task<ActionResult> GetSectionsSettings(string storeId, string theme)
        {
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
            if (!IsValidSchemaKind(kind))
            {
                return BadRequest(new { error = $"Unknown kind '{kind}'. Expected one of: {SchemaKindSections}, {SchemaKindTemplates}, {SchemaKindBlocks}, {SchemaKindObjects}, {SchemaKindShared}." });
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

        internal static bool IsValidSchemaKind(string kind)
        {
            return kind is SchemaKindSections or SchemaKindTemplates or SchemaKindBlocks or SchemaKindObjects or SchemaKindShared;
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
        public async Task<string> Search(string storeId, string theme, string type, string folder, string pattern = null, string keyword = null)
        {
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
            return result;
        }

        [HttpPost]
        [Route("save")]
        public async Task<ActionResult> SaveTemplates(string storeId, string theme, [FromBody] SaveFilesModel value, [FromQuery] bool draft = false)
        {
            var files = JsonConvert.DeserializeObject<List<SaveFileModel>>(value.Files);

            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                await SaveFilesTo(storeId, theme, files, draft);
                return Ok();
            }

            if (!draft)
            {
                // With the git flow on, live writes belong to the CI deploy (merge to the production
                // branch): only callers holding the publish permission (the CI service account) may
                // pass draft=false. Editors publish through PR + merge instead.
                var authorizationResult = await authorizationService.AuthorizeAsync(User, null, ModuleConstants.Security.Permissions.Publish);
                if (!authorizationResult.Succeeded)
                {
                    return Forbid();
                }

                await SaveFilesTo(storeId, theme, files, draft: false);
                return Ok();
            }

            // A draft is a commit on a work branch, not a .page-draft blob. The blob was a single slot
            // per page, so two editors of the same page overwrote each other's draft and preview; their
            // branches do not.
            var pages = files.Where(x => GitPageLocation.IsPageContent(x.Type)).ToList();

            var errors = pages
                .SelectMany(file => PageEnvelopeValidator.Validate(AsToken(file.Content)).Select(error => $"{file.Path}: {error}"))
                .ToList();
            if (errors.Count > 0)
            {
                return BadRequest(new { errors });
            }

            var saved = new List<object>();
            foreach (var page in pages)
            {
                saved.Add(await CommitPageToGitAsync(page, storeId));
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

        /// <summary>
        /// Publishes a page: merges its work branch into the production branch, from where CI deploys it.
        /// The editor's own draft is what ships — never the bytes the caller happens to send.
        /// </summary>
        [HttpPost]
        [Route("git/publish")]
        public async Task<ActionResult> GitPublish(string storeId, string path, string type)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                // the switch is off for this store: publish means what it always meant
                await publishingService.PublishingAsync(type ?? PagesContentType, storeId, path, publish: true);
                return Ok(new { state = nameof(GitPublishState.Merged) });
            }

            if (!await IsAllowedToPublishAsync())
            {
                return Forbid();
            }

            var location = GitLocation(type, path);

            var draft = await gitContentRepository.ReadFileAsync(location.RepoPath, location.Branch, HttpContext.RequestAborted);
            if (draft == null)
            {
                // no branch, or no such page on it: there is nothing of this editor's to publish
                return Ok(new { state = nameof(GitPublishState.AlreadyPublished) });
            }

            // The CI gate runs the full, schema-aware validator; this one only refuses to open a pull
            // request that could never pass it.
            var errors = PageEnvelopeValidator.Validate(ParsePage(draft));
            if (errors.Count > 0)
            {
                return BadRequest(new { errors });
            }

            var result = await gitContentPublisher.MergeBranchAsync(location.Branch, $"publish {path} (store: {storeId})", HttpContext.RequestAborted);

            return await RespondToPublishAsync(result, location, path);
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
        [Route("git/promote")]
        public async Task<ActionResult> GitPromote(string storeId, string path, string type)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                // without git there is no second branch to promote into
                return NotFound();
            }

            var options = gitContentOptions.Value;
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

            if (!await IsAllowedToPromoteAsync())
            {
                return Forbid();
            }

            var location = GitLocation(type, path);

            var onBase = await gitContentRepository.ReadFileAsync(location.RepoPath, options.BaseBranch, HttpContext.RequestAborted);
            if (onBase == null)
            {
                return BadRequest(new
                {
                    error = $"\"{path}\" is not on {options.BaseBranch}. Publish it there first — production " +
                            "follows what has already been through the dev environment.",
                });
            }

            var onRelease = await gitContentRepository.ReadFileAsync(location.RepoPath, options.ReleaseBranch, HttpContext.RequestAborted);
            if (onRelease != null && PageJson.AreSame(onBase, onRelease))
            {
                // production already holds this exact page; opening a pull request would only produce an
                // empty one, and the editor would be left waiting for a merge that means nothing
                return Ok(new { state = nameof(GitPublishState.AlreadyPublished) });
            }

            var branch = PromoteBranch(type, path);
            await PreparePromoteBranchAsync(branch, location.RepoPath, options.ReleaseBranch);

            // The bytes come from the base branch, never from the request: promotion ships what the dev
            // environment has been serving, not what a client happens to send along with the button press.
            await gitContentRepository.CommitFileAsync(
                location.RepoPath,
                onBase,
                branch,
                CommitMessage($"promote {path} to {options.ReleaseBranch} (store: {storeId})"),
                CurrentAuthor(),
                HttpContext.RequestAborted);

            var result = await gitContentPublisher.MergeBranchIntoAsync(
                branch, $"promote {path} (store: {storeId})", options.ReleaseBranch, HttpContext.RequestAborted);

            return await RespondToShipAsync(result, location.RepoPath, branch, options.ReleaseBranch, path);
        }

        /// <summary>
        /// Unpublishes a page by removing it from the production branch. Deleting the file is the whole
        /// operation here — the module never removes a page from blob storage itself, or production would
        /// stop matching the branch and a revert would stop being a rollback. CI carries the deletion to
        /// the environment on merge, the same way it carries every other change to the branch.
        /// </summary>
        [HttpPost]
        [Route("git/unpublish")]
        public async Task<ActionResult> GitUnpublish(string storeId, string path, string type)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                await publishingService.PublishingAsync(type ?? PagesContentType, storeId, path, publish: false);
                return Ok();
            }

            if (!await IsAllowedToPublishAsync())
            {
                return Forbid();
            }

            var options = gitContentOptions.Value;
            var location = GitLocation(type, path);

            if (await gitContentRepository.ReadFileAsync(location.RepoPath, options.BaseBranch, HttpContext.RequestAborted) == null)
            {
                return NotFound(new { templatePath = path });
            }

            var hadBranch = await gitContentRepository.GetBranchHeadShaAsync(location.Branch, HttpContext.RequestAborted) != null;
            if (!hadBranch)
            {
                await gitContentRepository.CreateBranchAsync(location.Branch, options.BaseBranch, HttpContext.RequestAborted);
            }

            // A branch cut from the production branch a moment ago still holds the page. One that was
            // already there may not: an earlier unpublish whose merge is waiting to be asked for again
            // has committed the deletion, and all that is left of this operation is the merge. Deleting
            // a file that is already gone is an error rather than a no-op, so that retry would fail here
            // instead of finishing the unpublish it is retrying.
            if (!hadBranch || await gitContentRepository.ReadFileAsync(location.RepoPath, location.Branch, HttpContext.RequestAborted) != null)
            {
                await gitContentRepository.DeleteFileAsync(location.RepoPath, location.Branch, CommitMessage($"unpublish {path} (store: {storeId})"), CurrentAuthor(), HttpContext.RequestAborted);
                gitContentHistory.Invalidate(location.RepoPath);
            }

            var result = await gitContentPublisher.MergeBranchAsync(location.Branch, $"unpublish {path} (store: {storeId})", HttpContext.RequestAborted);

            return await RespondToPublishAsync(result, location, path);
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
        public async Task<ActionResult> GitPublishStatus(string storeId, string path, string type)
        {
            var gitFlow = await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted);

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

            var options = gitContentOptions.Value;
            var location = GitLocation(type, path);

            var published = await gitContentRepository.ReadFileAsync(location.RepoPath, options.BaseBranch, HttpContext.RequestAborted);
            var draft = await gitContentRepository.ReadFileAsync(location.RepoPath, location.Branch, HttpContext.RequestAborted);
            var pending = await gitContentPublisher.GetOpenPullRequestAsync(location.Branch, HttpContext.RequestAborted);

            return Ok(PublishStatus(
                published: published != null,
                // a draft that says the same thing as production is not a change, whatever its history
                hasChanges: draft != null && !PageJson.AreSame(draft, published),
                pending: pending != null,
                gitFlow: true,
                // the pull request is open but nothing will merge it: publishing again is what finishes it
                awaitingMerge: pending is { AutoMerging: false },
                production: await ProductionStatusAsync(location.RepoPath, published, type, path)));
        }

        /// <summary>
        /// Where this page stands on the production branch, or <c>null</c> when the installation has none.
        /// <para>
        /// Reported separately from the fields above rather than folded into them: a page can be published
        /// and production still be serving last week's version of it, and that in-between state is the one
        /// that makes an editor say the site did not update. Nothing before this could express it.
        /// </para>
        /// </summary>
        private async Task<object> ProductionStatusAsync(string repoPath, string onBase, string type, string path)
        {
            var options = gitContentOptions.Value;
            if (string.IsNullOrWhiteSpace(options.ReleaseBranch))
            {
                return null;
            }

            var onRelease = await gitContentRepository.ReadFileAsync(repoPath, options.ReleaseBranch, HttpContext.RequestAborted);
            var pending = await gitContentPublisher.GetOpenPullRequestAsync(PromoteBranch(type, path), HttpContext.RequestAborted);

            return new
            {
                published = onRelease != null,
                // Behind only means something once the page is on the base branch at all: a page that has
                // never been published is not "missing from production", it simply has not started.
                behind = onBase != null && !PageJson.AreSame(onBase, onRelease),
                pending = pending != null,
                awaitingMerge = pending is { AutoMerging: false },
            };
        }

        private static object PublishStatus(bool published, bool hasChanges, bool pending, bool gitFlow,
            bool awaitingMerge = false, object production = null) => new
        {
            published,
            hasChanges,
            pending,
            // Told apart from pending on purpose: both mean "a pull request is open", but only this one
            // means the merge is not going to happen unless somebody asks for it again.
            awaitingMerge,
            flow = gitFlow ? GitFlow : BlobFlow,
            production,
        };

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
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                // there is no history to show: in the blob flow a draft overwrites the page and keeps no versions
                return NotFound();
            }

            var contentType = type.IsNullOrEmpty() ? PagesContentType : type;
            if (!GitPageLocation.IsPageContent(contentType))
            {
                // themes, schemas and settings never moved to git
                return NotFound();
            }

            if (path.IsNullOrEmpty())
            {
                // a page that does not exist yet — the blade asking about one it is about to create
                return Ok(PageHistoryModel.Empty);
            }

            var location = GitLocation(contentType, path);

            var history = await gitContentHistory.GetHistoryAsync(location.RepoPath, new GitHistoryQuery
            {
                BaseBranch = gitContentOptions.Value.BaseBranch,
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
        [Route("git/restore-version")]
        public async Task<ActionResult> GitRestoreVersion(string storeId, string path, string type, string sha)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                // without git there are no versions to continue from
                return NotFound();
            }

            var contentType = type.IsNullOrEmpty() ? PagesContentType : type;
            if (!GitPageLocation.IsPageContent(contentType) || path.IsNullOrEmpty())
            {
                return NotFound();
            }

            if (!GitCommitSha.IsSha(sha))
            {
                // a version is a commit; a branch name would mean "whatever is there when this runs"
                return BadRequest(new { error = $"\"{sha}\" is not a commit sha." });
            }

            var options = gitContentOptions.Value;
            var location = GitLocation(contentType, path);

            var content = await gitContentRepository.ReadFileAsync(location.RepoPath, sha, HttpContext.RequestAborted);
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

            if (await gitContentRepository.GetBranchHeadShaAsync(location.Branch, HttpContext.RequestAborted) == null)
            {
                await gitContentRepository.CreateBranchAsync(location.Branch, options.BaseBranch, HttpContext.RequestAborted);
            }

            var author = CurrentAuthor();
            var shortSha = sha[..7];
            var message = CommitMessage($"designer: restore {path} from {shortSha} (store: {storeId}, by: {author.Name})");

            // Canonical bytes, not the ones read from the commit: an older version may have been written
            // before the canonical form existed, and re-committing it verbatim would make publish-status
            // report changes that are only line endings.
            var commitSha = await gitContentRepository.CommitFileAsync(location.RepoPath, PageJson.Serialize(page), location.Branch, message, author, HttpContext.RequestAborted);
            gitContentHistory.Invalidate(location.RepoPath);

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
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                return NotFound();
            }

            // A version list offers a preview of one exact commit; asked for a ref, this endpoint hands it
            // straight to the storefront, so only a sha is accepted — a caller-chosen branch name would end
            // up in the redirect and stop meaning what the link said.
            if (requestedRef != null && !GitCommitSha.IsSha(requestedRef))
            {
                return BadRequest(new { error = $"\"{requestedRef}\" is not a commit sha." });
            }

            var location = GitLocation(type, path);

            var gitRef = requestedRef
                         ?? await gitContentRepository.GetBranchHeadShaAsync(location.Branch, HttpContext.RequestAborted)
                         ?? await gitContentRepository.GetBranchHeadShaAsync(gitContentOptions.Value.BaseBranch, HttpContext.RequestAborted);
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

        /// <summary>
        /// Throws this editor's draft of the page away, so the next read starts from what is published.
        /// The way out of a conflict, and the only one that does not quietly pick a winner.
        /// </summary>
        [HttpPost]
        [Route("git/discard-draft")]
        public async Task<ActionResult> GitDiscardDraft(string storeId, string path, string type)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                return NotFound();
            }

            var location = GitLocation(type, path);

            await gitContentRepository.DeleteBranchAsync(location.Branch, location.RepoPath, HttpContext.RequestAborted);
            // the draft is gone, and so are the versions that lived only on that branch
            gitContentHistory.Invalidate(location.RepoPath);

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
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
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
                items.Add(await DescribeLegacyDraftAsync(storageProvider, contentType, draft));
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
        [Route("git/legacy-drafts/delete")]
        public async Task<ActionResult> DeleteLegacyDrafts(string storeId, string type, string theme,
            [FromBody] LegacyDraftsDeleteRequest request)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                return NotFound();
            }

            if (!await IsAllowedToDeleteAsync())
            {
                return Forbid();
            }

            request ??= new LegacyDraftsDeleteRequest();

            var paths = (request.Paths ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // One bad path fails the whole batch instead of being skipped: a caller that got a path wrong
            // has a bug, and letting the rest through would hide it behind a half-finished cleanup.
            var invalid = paths.Where(x => !IsLegacyDraftPath(x)).ToList();
            if (invalid.Count > 0)
            {
                return BadRequest(new
                {
                    error = $"Only \"{DraftSuffix}\" files can be deleted here, by their exact path.",
                    paths = invalid,
                });
            }

            var contentType = type ?? PagesContentType;
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(
                GetContentBasePath(storeId, contentType, await GetCurrentThemeName(storeId, theme)));

            var (deletable, skipped) = await SortLegacyDraftsAsync(paths, storageProvider, contentType);

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
            IEnumerable<string> paths, IBlobContentStorageProvider storageProvider, string contentType)
        {
            var deletable = new List<string>();
            var skipped = new List<object>();

            foreach (var path in paths)
            {
                var repoPath = GitLocation(contentType, path).RepoPath;
                var inGit = await gitContentRepository.ReadFileAsync(repoPath, gitContentOptions.Value.BaseBranch, HttpContext.RequestAborted) != null;
                var isPublished = inGit || await storageProvider.GetBlobInfoAsync(WithoutDraftSuffix(path)) != null;

                if (isPublished)
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
        [Route("git/copy")]
        public async Task<ActionResult> GitCopyPage(string storeId, string type, string srcPath, string destPath)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                return NotFound();
            }

            if (!await IsAllowedToCreateAsync())
            {
                return Forbid();
            }

            if (srcPath.IsNullOrEmpty())
            {
                return BadRequest(new { error = "srcPath is required." });
            }

            var contentType = type ?? PagesContentType;
            // What the editor sees, which is their own draft of the source when they have one. Copying
            // the published version instead would silently duplicate something other than what is on
            // screen.
            var source = await ReadPageFromGitAsync(contentType, srcPath, draft: true, gitRef: null);
            if (source == null)
            {
                return BadRequest(new { error = $"\"{srcPath}\" is not in the content repository.", srcPath });
            }

            var target = destPath.IsNullOrEmpty()
                ? await NextFreeCopyPathAsync(contentType, srcPath)
                : destPath;
            if (target == null)
            {
                return BadRequest(new { error = $"\"{srcPath}\" already has {MaxCopyIndex} copies; name the next one yourself.", srcPath });
            }

            var saved = await CommitPageToGitAsync(
                new SaveFileModel { Path = target, Type = contentType, Content = JToken.Parse(source) },
                storeId,
                action: $"copy {WithoutDraftSuffix(srcPath)} to");

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
        private async Task<string> NextFreeCopyPathAsync(string contentType, string srcPath)
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
                if (!await PageExistsInRepositoryAsync(contentType, candidate))
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
        private async Task<bool> PageExistsInRepositoryAsync(string contentType, string path)
        {
            var location = GitLocation(contentType, path);

            return await gitContentRepository.ReadFileAsync(location.RepoPath, gitContentOptions.Value.BaseBranch, HttpContext.RequestAborted) != null
                || await gitContentRepository.ReadFileAsync(location.RepoPath, location.Branch, HttpContext.RequestAborted) != null;
        }

        /// <summary>The page's own name, whichever of the two names the caller happens to hold.</summary>
        private static string WithoutDraftSuffix(string path) =>
            path.EndsWith(DraftSuffix, StringComparison.OrdinalIgnoreCase) ? path[..^DraftSuffix.Length] : path;

        private async Task<bool> IsAllowedToCreateAsync()
        {
            var authorization = await authorizationService.AuthorizeAsync(User, null, ModuleConstants.Security.Permissions.Create);
            return authorization.Succeeded;
        }

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
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
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
                : await DescribeLegacyDraftAsync(storageProvider, contentType, blobInfo));
        }

        /// <summary>The draft blob name of a page, whichever of the two names the caller already holds.</summary>
        private static string LegacyDraftPathOf(string path) =>
            path.EndsWith(DraftSuffix, StringComparison.OrdinalIgnoreCase) ? path : path + DraftSuffix;

        /// <summary>
        /// A path this cleanup is allowed to touch: a "-draft" file, named outright. Nothing is resolved,
        /// completed or walked up from here — that is the whole point of the endpoint.
        /// </summary>
        private static bool IsLegacyDraftPath(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.EndsWith(DraftSuffix, StringComparison.OrdinalIgnoreCase) &&
            !path.Replace('\\', '/').Split('/').Contains("..");

        private async Task<LegacyDraftInfo> DescribeLegacyDraftAsync(IBlobContentStorageProvider storageProvider,
            string contentType, BlobEntry draft)
        {
            var repoPath = GitLocation(contentType, draft.RelativeUrl).RepoPath;
            var inGit = await gitContentRepository.ReadFileAsync(repoPath, gitContentOptions.Value.BaseBranch, HttpContext.RequestAborted);
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

        private async Task<bool> IsAllowedToDeleteAsync()
        {
            var authorization = await authorizationService.AuthorizeAsync(User, null, ModuleConstants.Security.Permissions.Delete);
            return authorization.Succeeded;
        }

        private Task<ActionResult> RespondToPublishAsync(GitPublishResult result, (string RepoPath, string Branch) location, string path) =>
            RespondToShipAsync(result, location.RepoPath, location.Branch, gitContentOptions.Value.BaseBranch, path);

        /// <summary>
        /// The tail every shipping operation shares — publish, unpublish and promote differ in which
        /// branch they merged into, and in nothing else once the merge has happened.
        /// </summary>
        private async Task<ActionResult> RespondToShipAsync(GitPublishResult result, string repoPath, string branch,
            string mergedInto, string path)
        {
            if (result.State == GitPublishState.Merged)
            {
                await gitContentRepository.DeleteBranchAsync(branch, repoPath, HttpContext.RequestAborted);

                // the merge moved that branch, so what it said about this page a moment ago is the
                // pre-merge content — status would report the page as still unshipped
                gitContentRepository.InvalidateRead(repoPath, mergedInto);
                // and the draft version that just shipped is now a published one
                gitContentHistory.Invalidate(repoPath);
            }

            if (result.State == GitPublishState.Conflict)
            {
                return Conflict(new
                {
                    error = $"\"{path}\" changed in the production branch while this draft was being written. Re-read the page and apply the change again.",
                    pullRequest = result.PullRequestNumber,
                    url = result.Url,
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
        private async Task PreparePromoteBranchAsync(string branch, string repoPath, string releaseBranch)
        {
            var leftover = await gitContentRepository.GetBranchHeadShaAsync(branch, HttpContext.RequestAborted);
            if (leftover != null &&
                await gitContentPublisher.GetOpenPullRequestAsync(branch, HttpContext.RequestAborted) != null)
            {
                return;
            }

            if (leftover != null)
            {
                await gitContentRepository.DeleteBranchAsync(branch, repoPath, HttpContext.RequestAborted);
            }

            await gitContentRepository.CreateBranchAsync(branch, releaseBranch, HttpContext.RequestAborted);
        }

        private async Task<bool> IsAllowedToPromoteAsync()
        {
            var authorization = await authorizationService.AuthorizeAsync(User, null, ModuleConstants.Security.Permissions.Promote);
            return authorization.Succeeded;
        }

        // Promotion is per page, not per editor: the branch carries no {user}, so two people promoting the
        // same page reuse one branch and one pull request instead of racing with two.
        private string PromoteBranch(string type, string path) =>
            GitPageLocation.BranchFor(gitContentOptions.Value.PromoteBranchTemplate, userName: null,
                GitPageLocation.ContentPath(type, path));

        private async Task<bool> IsAllowedToPublishAsync()
        {
            var authorization = await authorizationService.AuthorizeAsync(User, null, ModuleConstants.Security.Permissions.Publish);
            return authorization.Succeeded;
        }

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

        private async Task<object> CommitPageToGitAsync(SaveFileModel file, string storeId, string action = "save")
        {
            var options = gitContentOptions.Value;
            var (repoPath, branch) = GitLocation(file.Type, file.Path);

            // Cut from the production branch at the first save of this page, so what a merge of this
            // branch ships is today's published page plus this edit — never a stale base, never another
            // page the editor happens to be working on.
            if (await gitContentRepository.GetBranchHeadShaAsync(branch, HttpContext.RequestAborted) == null)
            {
                await gitContentRepository.CreateBranchAsync(branch, options.BaseBranch, HttpContext.RequestAborted);
            }

            var author = CurrentAuthor();
            // Canonical bytes, not JsonConvert's: publish status compares this branch against the
            // production branch, and Formatting.Indented would end lines with the host's newline.
            var content = PageJson.Serialize(file.Content);
            var message = CommitMessage($"designer: {action} {file.Path} (store: {storeId}, by: {author.Name})");

            // Authoritative, not best-effort: when the commit fails the save fails, because an editor
            // who was told their work is saved has to be able to find it.
            var commitSha = await gitContentRepository.CommitFileAsync(repoPath, content, branch, message, author, HttpContext.RequestAborted);

            // the page has a version it did not have a moment ago, and a cached list that omits the save
            // the editor has just been told succeeded is worse than a slow one
            gitContentHistory.Invalidate(repoPath);

            return new { path = file.Path, branch, commitSha };
        }

        /// <summary>
        /// A commit message with the authenticated login recorded under it. Git authorship is signed with
        /// a shared address, so this trailer is the only attribution that survives — and because the
        /// server writes it from the identity on the request, it is one a client cannot dress up.
        /// </summary>
        private string CommitMessage(string summary) => GitCommitMessage.WithVcUser(summary, User?.Identity?.Name);

        private GitCommitAuthor CurrentAuthor()
        {
            var options = gitContentOptions.Value;
            var userName = User?.Identity?.Name;

            return new GitCommitAuthor
            {
                Name = string.IsNullOrEmpty(userName) ? options.FallbackAuthorName : userName,
                Email = User?.FindFirstValue(ClaimTypes.Email) ?? options.FallbackAuthorEmail,
            };
        }

        private static JToken AsToken(object content)
        {
            return content as JToken ?? (content == null ? JValue.CreateNull() : JToken.FromObject(content));
        }

        private async Task SaveFilesTo(string storeId, string theme, IEnumerable<SaveFileModel> files, bool draft, CancellationToken cancellationToken = default)
        {
            var providers = new Dictionary<string, IBlobContentStorageProvider>();

            var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            var themeName = await GetCurrentThemeName(storeId, theme);

            var changedFiles = new Dictionary<string, List<GenericChangedEntry<FileEntity>>>();

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

                if (!changedFiles.TryGetValue(type, out var entries))
                {
                    entries = new List<GenericChangedEntry<FileEntity>>();
                    changedFiles.Add(type, entries);
                }

                entries.Add(new GenericChangedEntry<FileEntity>(new FileEntity
                {
                    Path = file.Path,
                    Type = file.Type
                }, EntryState.Modified));
            }
            changedFiles.Keys.ToList().ForEach(async x =>
            {
                await eventPublisher.Publish(new PageBuilderContentChangedEvent(x, changedFiles[x]), cancellationToken);
            });
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
            return store?.DynamicProperties.FirstOrDefault(x => x.Name == "DefaultThemeName")?.Values?.FirstOrDefault()?.Value?.ToString() ?? DefaultTheme;
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
