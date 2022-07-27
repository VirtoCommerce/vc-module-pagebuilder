using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.Platform.Core.Common;

//using VirtoCommerce.PageBuilderModule.Web.Models;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    [Route("api/pagebuilder")]
    // todo: temporary disable authorization
    //[Authorize]
    public class PageBuilderController : Controller
    {
        private readonly IBlobContentStorageProviderFactory _blobContentStorageProviderFactory;
        private readonly ContentOptions _options;

        private const string _blogsFolderName = "blogs";
        private const string _pages = "pages";
        private const string _themes = "themes";

        public PageBuilderController(
            IBlobContentStorageProviderFactory blobContentStorageProviderFactory,
            IOptions<ContentOptions> options)
        {
            _blobContentStorageProviderFactory = blobContentStorageProviderFactory;
            _options = options.Value;
        }

        [HttpGet]
        [Route("template")]
        public async Task<ActionResult> GetTemplate(string storeId, string path, string type)
        {
            var basePath = GetContentBasePath(storeId, type);
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(basePath);

            // todo: it's a problem to check for type
            if (type == _themes)
            {
                path = "/default/" + (path.StartsWith("/") ? path.Substring(1) : path);
            }

            if ((await storageProvider.GetBlobInfoAsync(path)) != null)
            {
                var stream = await storageProvider.OpenReadAsync(path);
                return File(stream, MimeTypeResolver.ResolveContentType(path));
            }
            return NotFound(new
            {
                basePath = basePath,
                templatePath = path
            });
        }

        [HttpGet]
        [Route("templates")]
        public async Task<ActionResult> GetTemplates(string storeId)
        {
            var result = await GetFilesFromFolder(storeId, "templates", _themes);
            return Content(result, "application/json");
        }

        [HttpGet]
        [Route("objects")]
        public async Task<ActionResult> GetObjects(string storeId)
        {
            var result = await GetFilesFromFolder(storeId, "objects", _themes);
            return Ok(result);
        }

        [HttpGet]
        [Route("sections")]
        public async Task<ActionResult> GetSectionsSettings(string storeId)
        {
            var sections = await GetFilesFromFolder(storeId, "sections", _themes);
            var blocks = await GetFilesFromFolder(storeId, "blocks", _themes);
            var objects = await GetFilesFromFolder(storeId, "objects", _themes);
            return Content($"{{ \"sections\": {sections}, \"blocks\": {blocks}, \"objects\": {objects} }}", "application/json");
        }

        // todo: create model for files and descriptors (template entry)
        [HttpGet]
        [Route("search")]
        public async Task<string> Search(string storeId, string query, string folder, string type)
        {
            var templatesFolder = $"/default/content/{folder}";
            var basePath = GetContentBasePath(storeId, type);
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(basePath);
            var searchPattern = $"{query}.(json|page|template)";
            var files = (await storageProvider.SearchAsync(templatesFolder, query)).Results.Where(x => x.Type != "folder").Take(10);
            var fileInfoes = new Dictionary<string, string>();
            var jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            foreach (var file in files)
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
        public async Task<ActionResult> SaveTemplates(string storeId, [FromBody] SaveFilesModel value)
        {
            await SaveFilesTo(storeId, value);
            return Ok();
        }

        private async Task SaveFilesTo(string storeId, SaveFilesModel model)
        {
            var files = JsonConvert.DeserializeObject<IEnumerable<SaveFileModel>>(model.Files);

            var providers = new Dictionary<string, IBlobContentStorageProvider>();

            var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            foreach (var file in files)
            {
                var type = file.Type.ToLowerInvariant();
                var storageProvider = providers.ContainsKey(type)
                    ? providers[type]
                    : (providers[type] = _blobContentStorageProviderFactory.CreateProvider(GetContentBasePath(storeId, type)));
                var filepath = "/default/" + (file.Path.StartsWith("/") ? file.Path.Substring(1) : file.Path);
                var content = file.Content;
                await using var targetStream = await storageProvider.OpenWriteAsync(filepath);
                await using var writer = new StreamWriter(targetStream);
                var stringContent = JsonConvert.SerializeObject(content, settings);
                await writer.WriteAsync(stringContent);
            }
        }

        private async Task<string> GetFilesFromFolder(string storeId, string folder, string type)
        {
            var templatesFolder = $"/default/config/schemas/{folder}";
            var basePath = GetContentBasePath(storeId, type);
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(basePath);
            var files = await storageProvider.SearchAsync(templatesFolder, "*.json");
            var result = $"{{{string.Join(", ", files.Results.Select(file => $"\"{GetKey(file)}\": {GetContent(file, storageProvider)}"))}}}";
            return result;
        }

        private string GetKey(BlobEntry entry)
        {
            // todo: can be situation when files have the same name in different folders. can be source of problem
            return Path.GetFileNameWithoutExtension(entry.Name);
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

        private string GetContentBasePath(string storeId, string contentType)
        {
            //var result = $"{type}/{storeId}";
            //return result;
            //if (_options.TypeMappings != null && _options.TypeMappings.Count() > 0 && _options.TypeMappings.ContainsKey(contentType))
            //{
            //    var mapping = _options.TypeMappings[contentType];
            //    return string.Join('/', mapping.Select(x => x == "_storeId" ? storeId : x));
            //}

            var retVal = string.Empty;
            if (contentType.EqualsInvariant(_themes))
            {
                retVal = "Themes/" + storeId;
            }
            else if (contentType.EqualsInvariant(_pages))
            {
                retVal = "Pages/" + storeId;
            }
            else if (contentType.EqualsInvariant(_blogsFolderName))
            {
                retVal = "Pages/" + storeId + $"/{_blogsFolderName}";
            }

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
