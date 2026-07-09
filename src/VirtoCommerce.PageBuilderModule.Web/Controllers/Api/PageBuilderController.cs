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
            IAuthorizationService authorizationService
            )
        : Controller
    {
        private const string Themes = "themes";
        // the only content type the git flow covers: blogs are markdown articles, themes and schemas
        // stay in blob storage
        private const string PagesContentType = "pages";
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
            if (IsPage(type) && !path.IsNullOrEmpty() &&
                await gitContentPolicy.IsEnabledForStoreAsync(storeId, HttpContext.RequestAborted))
            {
                return await GetTemplateFromGit(path, draft, gitRef);
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
        /// Reads a page out of the content repository. Without an explicit ref: the editor's draft of
        /// this page when they have one, otherwise the published version. That fallback is what keeps
        /// the contract of the blob-backed endpoint — "the draft, else what is live" — intact, so the
        /// callers of this URL never learn that pages moved to git.
        /// </summary>
        private async Task<ActionResult> GetTemplateFromGit(string path, bool draft, string gitRef)
        {
            var options = gitContentOptions.Value;
            var repoPath = GitPageLocation.RepoPath(options.PagesRoot, path);

            if (!string.IsNullOrEmpty(gitRef))
            {
                // an exact commit — what a preview link points at
                var atRef = await gitContentRepository.ReadFileAsync(repoPath, gitRef, HttpContext.RequestAborted);
                return atRef == null ? NotFound(new { templatePath = path, gitRef }) : PageContent(atRef);
            }

            if (draft)
            {
                var branch = GitPageLocation.BranchFor(options.BranchTemplate, User?.Identity?.Name, path);
                var onBranch = await gitContentRepository.ReadFileAsync(repoPath, branch, HttpContext.RequestAborted);
                if (onBranch != null)
                {
                    return PageContent(onBranch);
                }
            }

            var published = await gitContentRepository.ReadFileAsync(repoPath, options.BaseBranch, HttpContext.RequestAborted);
            return published == null ? NotFound(new { templatePath = path }) : PageContent(published);
        }

        private ContentResult PageContent(string json) => Content(json, JsonContentType);

        private static bool IsPage(string contentType) => string.Equals(contentType, PagesContentType, StringComparison.OrdinalIgnoreCase);

        [HttpGet]
        [Route("settings")]
        public async Task<ActionResult> GetSettings(string storeId, string theme)
        {
            var themeName = await GetCurrentThemeName(storeId, theme);
            var filePath = $"{themeName}/config/builder_settings.json";
            var basePath = GetContentBasePath(storeId, Themes, themeName);
            var storageProvider = blobContentStorageProviderFactory.CreateProvider(basePath);

            var blobInfo = await storageProvider.GetBlobInfoAsync(filePath);

            if (blobInfo != null)
            {
                var stream = await storageProvider.OpenReadAsync(blobInfo.RelativeUrl);
                return File(stream, MimeTypeResolver.ResolveContentType(blobInfo.Name));
            }
            return Content("{}");
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
            var pages = files.Where(x => string.Equals(x.Type, PagesContentType, StringComparison.OrdinalIgnoreCase)).ToList();

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

            // Themes, schemas and everything else still live in blob storage — only pages moved to git.
            var others = files.Except(pages).ToList();
            if (others.Count > 0)
            {
                await SaveFilesTo(storeId, theme, others, draft: true);
            }

            return Ok(new { pages = saved });
        }

        private async Task<object> CommitPageToGitAsync(SaveFileModel file, string storeId)
        {
            var options = gitContentOptions.Value;
            var branch = GitPageLocation.BranchFor(options.BranchTemplate, User?.Identity?.Name, file.Path);
            var repoPath = GitPageLocation.RepoPath(options.PagesRoot, file.Path);

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
