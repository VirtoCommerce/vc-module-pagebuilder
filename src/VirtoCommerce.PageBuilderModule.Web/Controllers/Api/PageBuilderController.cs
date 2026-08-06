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
        private const string DefaultTheme = "default";
        private const string JsonContentType = "application/json";
        private const string JsonExtension = ".json";
        private const string SchemaKindSections = "sections";
        private const string SchemaKindTemplates = "templates";
        private const string SchemaKindBlocks = "blocks";
        private const string SchemaKindObjects = "objects";
        private const string SchemaKindShared = "shared";


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
        /// The publish-related request descriptors for a store on the git flow. Note the absence of
        /// <c>unpublish</c>: with pages in git, taking a page down is deleting it from the production
        /// branch, and the toolbar hides the button when no descriptor is offered.
        /// </summary>
        private static JObject GitBuilderDescriptors()
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
                },
                ["externalPreview"] = new JObject
                {
                    ["url"] = $"/api/pagebuilder/git/preview?{storeIdArg}&{pageArgs}",
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
        /// Unpublishes a page by removing it from the production branch. Deleting the file is the whole
        /// operation — the module never removes a page from blob storage itself, or production would
        /// stop matching the branch and a revert would stop being a rollback.
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

            if (await gitContentRepository.GetBranchHeadShaAsync(location.Branch, HttpContext.RequestAborted) == null)
            {
                await gitContentRepository.CreateBranchAsync(location.Branch, options.BaseBranch, HttpContext.RequestAborted);
            }

            await gitContentRepository.DeleteFileAsync(location.RepoPath, location.Branch, $"unpublish {path} (store: {storeId})", CurrentAuthor(), HttpContext.RequestAborted);
            var result = await gitContentPublisher.MergeBranchAsync(location.Branch, $"unpublish {path} (store: {storeId})", HttpContext.RequestAborted);

            return await RespondToPublishAsync(result, location, path);
        }

        /// <summary>
        /// The shape the builder's toolbar expects, answered from git: <c>published</c> is "the page
        /// exists in the production branch", <c>hasChanges</c> is "this editor's branch says something
        /// different", <c>pending</c> is "a pull request for it is open".
        /// </summary>
        [HttpGet]
        [Route("git/publish-status")]
        public async Task<ActionResult> GitPublishStatus(string storeId, string path, string type)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                var status = await publishingService.PublishStatusAsync(type ?? PagesContentType, storeId, path);
                return Ok(status);
            }

            var location = GitLocation(type, path);

            var published = await gitContentRepository.ReadFileAsync(location.RepoPath, gitContentOptions.Value.BaseBranch, HttpContext.RequestAborted);
            var draft = await gitContentRepository.ReadFileAsync(location.RepoPath, location.Branch, HttpContext.RequestAborted);
            var pending = await gitContentPublisher.GetOpenPullRequestNumberAsync(location.Branch, HttpContext.RequestAborted);

            return Ok(new
            {
                published = published != null,
                // a draft that says the same thing as production is not a change, whatever its history
                hasChanges = draft != null && !PageJson.AreSame(draft, published),
                pending = pending != null,
            });
        }

        /// <summary>
        /// Redirects to the storefront's preview of this page at an exact commit: the editor's draft if
        /// they have one, otherwise what is published. A commit sha is immutable, so the link keeps
        /// showing what it showed when it was made.
        /// </summary>
        [HttpGet]
        [Route("git/preview")]
        public async Task<ActionResult> GitPreview(string storeId, string path, string type)
        {
            if (!await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                return NotFound();
            }

            var location = GitLocation(type, path);

            var gitRef = await gitContentRepository.GetBranchHeadShaAsync(location.Branch, HttpContext.RequestAborted)
                         ?? await gitContentRepository.GetBranchHeadShaAsync(gitContentOptions.Value.BaseBranch, HttpContext.RequestAborted);
            if (gitRef == null)
            {
                return NotFound(new { templatePath = path });
            }

            var storeUrl = await GetSettingAsync(ModuleConstants.Settings.General.StoreUrl);
            if (string.IsNullOrEmpty(storeUrl))
            {
                return BadRequest(new { error = $"Set {ModuleConstants.Settings.General.StoreUrl.Name} to the storefront url before previewing." });
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
            return Ok();
        }

        private async Task<ActionResult> RespondToPublishAsync(GitPublishResult result, (string RepoPath, string Branch) location, string path)
        {
            if (result.State == GitPublishState.Merged)
            {
                await gitContentRepository.DeleteBranchAsync(location.Branch, location.RepoPath, HttpContext.RequestAborted);

                // the merge moved the production branch, so what it said about this page a moment ago is
                // the pre-publish content — publish-status would report the page as still unpublished
                gitContentRepository.InvalidateRead(location.RepoPath, gitContentOptions.Value.BaseBranch);
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

        private async Task<bool> IsAllowedToPublishAsync()
        {
            var authorization = await authorizationService.AuthorizeAsync(User, null, ModuleConstants.Security.Permissions.Publish);
            return authorization.Succeeded;
        }

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

        private async Task<object> CommitPageToGitAsync(SaveFileModel file, string storeId)
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
            var message = $"designer: save {file.Path} (store: {storeId}, by: {author.Name})";

            // Authoritative, not best-effort: when the commit fails the save fails, because an editor
            // who was told their work is saved has to be able to find it.
            var commitSha = await gitContentRepository.CommitFileAsync(repoPath, content, branch, message, author, HttpContext.RequestAborted);

            return new { path = file.Path, branch, commitSha };
        }

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
