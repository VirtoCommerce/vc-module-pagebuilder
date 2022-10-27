using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Web.Events;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    [Route("api/pagebuilder")]
    [Authorize]
    public class PageBuilderController : Controller
    {
        private readonly ICrudService<Store> _storeService;
        private readonly IBlobContentStorageProviderFactory _blobContentStorageProviderFactory;
        private readonly ContentOptions _options;
        private readonly IEventPublisher _eventPublisher;

        private const string _blogsFolderName = "blogs";
        private const string _pages = "pages";
        private const string _themes = "themes";
        private const string _defaultTheme = "default";

        public PageBuilderController(
            IStoreService storeService,
            IBlobContentStorageProviderFactory blobContentStorageProviderFactory,
            IOptions<ContentOptions> options,
            IEventPublisher eventPublisher)
        {
            _storeService = (ICrudService<Store>)storeService;
            _blobContentStorageProviderFactory = blobContentStorageProviderFactory;
            _options = options.Value;
            _eventPublisher = eventPublisher;
        }

        [HttpGet]
        [Route("template")]
        public async Task<ActionResult> GetTemplate(string storeId, string theme, string path, string type)
        {
            var basePath = GetContentBasePath(storeId, type, theme);
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(basePath);

            var blobInfo = await storageProvider.GetBlobInfoAsync(path);

            if (blobInfo != null)
            {
                var stream = await storageProvider.OpenReadAsync(blobInfo.RelativeUrl);
                return File(stream, MimeTypeResolver.ResolveContentType(blobInfo.Name));
            }
            return NotFound(new
            {
                basePath = basePath,
                templatePath = path
            });
        }

        [HttpGet]
        [Route("settings")]
        public async Task<ActionResult> GetSettings(string storeId, string theme)
        {
            var themeName = GetCurrentThemeName(storeId, theme);
            var filePath = $"{themeName}/config/builder_settings.json";
            var basePath = GetContentBasePath(storeId, _themes, themeName);
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(basePath);

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
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(basePath);
            //var searchPattern = $"{query}.(json|page|template)"; // todo: use pattern correctly (search by filename? search by name from settings? elastic?)
            var regexp = pattern == null ? null : new Regex(pattern);
            var files = (await storageProvider.SearchAsync(folder, keyword))
                .Results.Where(x => x.Type != "folder" && (regexp?.IsMatch(x.Name) ?? true));
            var fileInfoes = new Dictionary<string, string>();
            var jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            foreach (var file in files)
            {
                try
                {
                    var key = GetKey(file);
                    if (!fileInfoes.ContainsKey(key))
                    {
                        var pageContent = GetPageContent(file, storageProvider);
                        if (pageContent != null)
                        {
                            var content = JsonConvert.SerializeObject(pageContent, jsonSettings);
                            fileInfoes.Add(key, content);
                        }
                    }
                }
                catch { }
            }
            var result = $"{{{string.Join(", ", fileInfoes.Keys.Select(x => $"\"{x}\": {fileInfoes[x]}"))}}}";
            return result;
        }

        //[HttpPost]
        //[Route("save-template-draft")]
        //public async Task<ActionResult> SaveTemplateDraft(string storeId, [FromBody] SaveFilesModel value)
        //{
        //    await SaveFilesTo(storeId, value, "/default/config/drafts");
        //    return Ok();
        //}

        [HttpPost]
        [Route("save")]
        public async Task<ActionResult> SaveTemplates(string storeId, string theme, [FromBody] SaveFilesModel value)
        {
            await SaveFilesTo(storeId, theme, value);
            return Ok();
        }

        private async Task SaveFilesTo(string storeId, string theme, SaveFilesModel model)
        {
            var files = JsonConvert.DeserializeObject<IEnumerable<SaveFileModel>>(model.Files);

            var providers = new Dictionary<string, IBlobContentStorageProvider>();

            var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            var themeName = GetCurrentThemeName(storeId, theme);

            var changedFiles = new Dictionary<string, List<GenericChangedEntry<FileEntity>>>();

            foreach (var file in files)
            {
                var type = file.Type.ToLowerInvariant();
                var storageProvider = providers.ContainsKey(type)
                    ? providers[type]
                    : (providers[type] = _blobContentStorageProviderFactory.CreateProvider(GetContentBasePath(storeId, type, themeName)));
                var content = file.Content;
                await using var targetStream = await storageProvider.OpenWriteAsync(file.Path);
                await using var writer = new StreamWriter(targetStream);
                var stringContent = JsonConvert.SerializeObject(content, settings);
                await writer.WriteAsync(stringContent);

                if (!changedFiles.ContainsKey(type))
                {
                    changedFiles.Add(type, new List<GenericChangedEntry<FileEntity>>());
                }

                changedFiles[type].Add(new GenericChangedEntry<FileEntity>(new FileEntity
                {
                    Id = file.Path,
                    Path = file.Path,
                    Type = file.Type
                }, EntryState.Modified));
            }

            changedFiles.Keys.ToList().ForEach(async x =>
            {
                await _eventPublisher.Publish(new PageBuilderContentChangedEvent(x, changedFiles[x]));
            });
        }

        private async Task<string> GetSettingsFilesFromFolder(string storeId, string theme, string folder)
        {
            var themeName = GetCurrentThemeName(storeId, theme);
            var templatesFolder = $"{themeName}/config/schemas/{folder}";
            var basePath = GetContentBasePath(storeId, _themes, themeName);
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(basePath);
            var allFiles = await storageProvider.SearchAsync(templatesFolder, null);
            var files = allFiles.Results.Where(x => x.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            var response = string.Join(", ", files.Select(file => $"\"{GetKey(file)}\": {GetContent(file, storageProvider)}"));
            var result = $"{{{response}}}";
            return result;
        }

        private string GetKey(BlobEntry entry)
        {
            // todo: can be situation when files have the same name in different folders. can be source of problem
            return Path.GetFileNameWithoutExtension(entry.Name);
        }

        private string GetCurrentThemeName(string storeId, string givenTheme)
        {
            if (!string.IsNullOrEmpty(givenTheme))
            {
                return givenTheme;
            }
            var store = _storeService.GetByIdAsync(storeId, StoreResponseGroup.DynamicProperties.ToString()).Result;
            var themeName = store.DynamicProperties.FirstOrDefault(x => x.Name == "DefaultThemeName")?.Values?.FirstOrDefault()?.Value.ToString() ?? _defaultTheme;
            return themeName;
        }

        private ContentModel GetPageContent(BlobEntry entry, IBlobContentStorageProvider provider)
        {
            try
            {
                using var reader = new StreamReader(provider.OpenRead(entry.Url));
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
            using var reader = new StreamReader(provider.OpenRead(entry.Url));
            return reader.ReadToEnd();
        }

        private string GetContentBasePath(string storeId, string contentType, string theme)
        {
            if (_options.PathMappings != null && _options.PathMappings.Any() && _options.PathMappings.ContainsKey(contentType))
            {
                var themeName = _defaultTheme;
                var mapping = _options.PathMappings[contentType];
                var parts = mapping.Select(x => x switch
                {
                    "_storeId" => storeId,
                    "_theme" => themeName,
                    "_blog" => _blogsFolderName,
                    _ => x,
                });
                var result = string.Join('/', parts);
                return result;
            }

            var retVal = contentType switch
            {
                var x when x.EqualsInvariant(_themes) => $"Themes/{storeId}",
                var x when x.EqualsInvariant(_pages) => $"Pages/{storeId}",
                var x when x.EqualsInvariant(_blogsFolderName) => $"Pages/{storeId}/{_blogsFolderName}",
                var x => string.Empty
            };

            return retVal;

        }

        public class SaveFilesModel
        {
            public string Files { get; set; }
        }

        public class SaveFileModel
        {
            public string Path { get; set; }
            public string Type { get; set; }
            public JObject Content { get; set; }
        }
    }
}
