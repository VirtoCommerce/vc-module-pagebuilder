using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.Validation;
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
            IAuthorizationService authorizationService,
            IValidator<PageBuilderContentRequest> requestValidator
            )
        : Controller
    {
        private const string Themes = "themes";
        private const string PagesContentType = "pages";
        private const string BlobFlow = "blob";
        private const string PublishedState = "Merged";
        private const string DefaultTheme = "default";
        private const string JsonContentType = "application/json";


        [HttpGet]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
        [Route("template")]
        public async Task<ActionResult> GetTemplate(string storeId, string theme, string path, string type, bool draft = false)
        {
            var request = new PageBuilderContentRequest { StoreId = storeId, ContentType = type, Theme = theme, Paths = PathsOf(path) };
            if (await CheckRequestAsync(request) is { } refused)
            {
                return refused;
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

        [HttpGet]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
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

            if (blobInfo != null)
            {
                var stream = await storageProvider.OpenReadAsync(blobInfo.RelativeUrl);
                return File(stream, MimeTypeResolver.ResolveContentType(blobInfo.Name));
            }
            return Content("{}");
        }

        [HttpGet]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
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
        [Authorize(ModuleConstants.Security.Permissions.Read)]
        [Route("objects")]
        public async Task<ActionResult> GetObjects(string storeId, string theme)
        {
            if (await CheckRequestAsync(storeId, Themes, theme) is { } refused)
            {
                return refused;
            }

            var result = await GetSettingsFilesFromFolder(storeId, theme, "objects");
            return Ok(result);
        }

        [HttpGet]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
        [Route("sections")]
        public async Task<ActionResult> GetSectionsSettings(string storeId, string theme)
        {
            if (await CheckRequestAsync(storeId, Themes, theme) is { } refused)
            {
                return refused;
            }

            var sections = await GetSettingsFilesFromFolder(storeId, theme, "sections");
            var blocks = await GetSettingsFilesFromFolder(storeId, theme, "blocks");
            var objects = await GetSettingsFilesFromFolder(storeId, theme, "objects");
            var shared = await GetSettingsFilesFromFolder(storeId, theme, "shared");
            return Content($"{{ \"sections\": {sections}, \"blocks\": {blocks}, \"objects\": {objects}, \"shared\": {shared} }}", JsonContentType);
        }

        [HttpGet]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
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
        public async Task<ActionResult> SaveTemplates(string storeId, string theme, [FromBody] SaveFilesModel value, [FromQuery] bool draft = false)
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

            if (IsPublication(files, draft) && !await IsAllowedToPublishAsync())
            {
                return Forbid();
            }

            request.Files = files.ConvertAll(ToContentFile);
            if (await ValidateAsync(request) is { } invalid)
            {
                return invalid;
            }

            await SaveFilesTo(storeId, theme, files, draft);
            return Ok();
        }

        // The designer saves theme settings straight to live under the update permission.
        private static bool IsPublication(List<SaveFileModel> files, bool draft) =>
            !draft && files.Exists(file => file != null && PageBuilderContentPaths.IsPageContentType(file.Type));

        private async Task<bool> IsAllowedToPublishAsync() =>
            (await authorizationService.AuthorizeAsync(User, null, ModuleConstants.Security.Permissions.Publish)).Succeeded;

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

        // The git/ routes and answers match the module versions that publish through git, so the designer
        // settings and the admin page blade are the same on every version; here they publish the blob.
        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Publish)]
        [Route("git/publish")]
        public async Task<ActionResult> GitPublish(string storeId, string path, string type)
        {
            if (await CheckRequestAsync(PageRequest(storeId, type, path)) is { } refused)
            {
                return refused;
            }

            await publishingService.PublishingAsync(type ?? PagesContentType, storeId, path, publish: true);
            return Ok(new { state = PublishedState });
        }

        [HttpPost]
        [Authorize(ModuleConstants.Security.Permissions.Publish)]
        [Route("git/unpublish")]
        public async Task<ActionResult> GitUnpublish(string storeId, string path, string type)
        {
            if (await CheckRequestAsync(PageRequest(storeId, type, path)) is { } refused)
            {
                return refused;
            }

            await publishingService.PublishingAsync(type ?? PagesContentType, storeId, path, publish: false);
            return Ok();
        }

        [HttpGet]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
        [Route("git/publish-status")]
        public async Task<ActionResult> GitPublishStatus(string storeId, string path, string type)
        {
            if (await CheckRequestAsync(PageRequest(storeId, type, PathsOf(path))) is { } refused)
            {
                return refused;
            }

            // A page that does not exist yet has no status.
            if (path.IsNullOrEmpty())
            {
                return Ok(PublishStatus(published: false, hasChanges: false));
            }

            var status = await publishingService.PublishStatusAsync(type ?? PagesContentType, storeId, path);
            return Ok(PublishStatus(status.Published, status.HasChanges));
        }

        private static object PublishStatus(bool published, bool hasChanges) => new
        {
            published,
            hasChanges,
            pending = false,
            awaitingMerge = false,
            flow = BlobFlow,
            production = (object)null,
        };

        private static string[] PathsOf(params string[] paths) => paths.Where(path => !path.IsNullOrEmpty()).ToArray();

        private static PageBuilderContentRequest PageRequest(string storeId, string type, params string[] pages) =>
            new() { StoreId = storeId, ContentType = type ?? PagesContentType, Pages = pages };

        private Task<ActionResult> CheckRequestAsync(string storeId, string type, string theme) =>
            CheckRequestAsync(new PageBuilderContentRequest
            {
                StoreId = storeId,
                ContentType = type,
                Theme = theme,
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
            // What did not bind, a malformed body or a query value of the wrong type, is the caller's mistake too.
            var bindingErrors = ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "The request could not be read." : error.ErrorMessage)
                .ToList();
            if (bindingErrors.Count > 0)
            {
                return BadRequest(new { error = string.Join(" ", bindingErrors) });
            }

            var validation = await requestValidator.ValidateAsync(request, HttpContext.RequestAborted);

            return validation.IsValid
                ? null
                : BadRequest(new { error = string.Join(" ", validation.Errors.Select(x => x.ErrorMessage)) });
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
            var files = allFiles.Results.Where(x => x.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            var response = string.Join(", ", files.Select(file => $"\"{GetKey(null, file)}\": {GetContent(file, storageProvider)}"));
            var result = $"{{{response}}}";
            return result;
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
