using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Events;
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
            IEventPublisher eventPublisher
            )
        : Controller
    {
        private const string Themes = "themes";
        private const string DefaultTheme = "default";


        [HttpGet]
        [Route("template")]
        public async Task<ActionResult> GetTemplate(string storeId, string theme, string path, string type, bool draft = false)
        {
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
            return Content(result, "application/json");
        }

        [HttpGet]
        [Route("objects")]
        public async Task<ActionResult> GetObjects(string storeId, string theme)
        {
            var result = await GetSettingsFilesFromFolder(storeId, theme, "objects");
            return Ok(result);
        }

        [HttpGet]
        [Route("sections")]
        public async Task<ActionResult> GetSectionsSettings(string storeId, string theme)
        {
            var sections = await GetSettingsFilesFromFolder(storeId, theme, "sections");
            var blocks = await GetSettingsFilesFromFolder(storeId, theme, "blocks");
            var objects = await GetSettingsFilesFromFolder(storeId, theme, "objects");
            var shared = await GetSettingsFilesFromFolder(storeId, theme, "shared");
            return Content($"{{ \"sections\": {sections}, \"blocks\": {blocks}, \"objects\": {objects}, \"shared\": {shared} }}", "application/json");
        }

        // todo: create model for files and descriptors (template entry)
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
            await SaveFilesTo(storeId, theme, value, draft);
            return Ok();
        }

        private async Task SaveFilesTo(string storeId, string theme, SaveFilesModel model, bool draft, CancellationToken cancellationToken = default)
        {
            var files = JsonConvert.DeserializeObject<IEnumerable<SaveFileModel>>(model.Files);

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
