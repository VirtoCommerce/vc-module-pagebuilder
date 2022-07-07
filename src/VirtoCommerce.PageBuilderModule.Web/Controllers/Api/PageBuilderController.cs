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
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Services;
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

            if ((await storageProvider.GetBlobInfoAsync(path)) != null)
            {
                var stream = await storageProvider.OpenReadAsync(path);
                return File(stream, MimeTypeResolver.ResolveContentType(path));
            }
            return NotFound(new 
            {
                basePath = basePath, templatePath = path
            });
        }

        [HttpGet]
        [Route("templates")]
        public async Task<ActionResult> GetTemplates(string storeId)
        {
            var result = await GetFilesFromFolder(storeId, "templates", "Themes");
            return Content(result, "application/json");
        }

        [HttpGet]
        [Route("sections")]
        public async Task<ActionResult> GetSectionsSettings(string storeId)
        {
            var sections = await GetFilesFromFolder(storeId, "sections", "Themes");
            var blocks = await GetFilesFromFolder(storeId, "blocks", "Themes");
            return Content($"{{ \"sections\": {sections}, \"blocks\": {blocks} }}", "application/json");
        }

        [HttpGet]
        [Route("objects")]
        public async Task<ActionResult> GetObjects(string storeId)
        {
            var result = await GetFilesFromFolder(storeId, "objects", "Themes");
            return Ok(result);
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
                var storageProvider = providers.ContainsKey(file.Type)
                    ? providers[file.Type]
                    : (providers[file.Type] = _blobContentStorageProviderFactory.CreateProvider(GetContentBasePath(storeId, file.Type)));
                var filepath = file.Path;
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
            return Path.GetFileNameWithoutExtension(entry.Name);
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
            if (_options.TypeMappings != null && _options.TypeMappings.Count() > 0 && _options.TypeMappings.ContainsKey(contentType))
            {
                var mapping = _options.TypeMappings[contentType];
                return string.Join('/', mapping.Select(x => x == "_storeId" ? storeId : x));
            }

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
