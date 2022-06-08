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
        public PageBuilderController(IBlobContentStorageProviderFactory blobContentStorageProviderFactory)
        {
            _blobContentStorageProviderFactory = blobContentStorageProviderFactory;
        }

        [HttpGet]
        [Route("template")]
        public async Task<ActionResult> GetTemplate(string storeId, string path)
        {
            var templatePath = $"/default/{path}";
            var basePath = GetContentBasePath(storeId);
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(basePath);

            if ((await storageProvider.GetBlobInfoAsync(templatePath)) != null)
            {
                var stream = await storageProvider.OpenReadAsync(templatePath);
                return File(stream, MimeTypeResolver.ResolveContentType(templatePath));
            }
            return NotFound(new 
            {
                basePath = basePath, templatePath = templatePath
            });
        }

        [HttpGet]
        [Route("templates")]
        public async Task<ActionResult> GetTemplates(string storeId)
        {
            var result = await GetFilesFromFolder(storeId, "templates");
            return Content(result, "application/json");
        }

        [HttpGet]
        [Route("sections")]
        public async Task<ActionResult> GetSectionsSettings(string storeId)
        {
            var sections = await GetFilesFromFolder(storeId, "sections");
            var blocks = await GetFilesFromFolder(storeId, "blocks");
            return Content($"{{ \"sections\": {sections}, \"blocks\": {blocks} }}", "application/json");
        }

        [HttpGet]
        [Route("objects")]
        public async Task<ActionResult> GetObjects(string storeId)
        {
            var result = await GetFilesFromFolder(storeId, "objects");
            return Ok(result);
        }

        [HttpPost]
        [Route("save-template-draft")]
        public async Task<ActionResult> SaveTemplateDraft(string storeId, [FromBody] SaveFilesModel value)
        {
            await SaveFilesTo(storeId, value, "/default/config/drafts");
            return Ok();
        }

        [HttpPost]
        [Route("save")]
        public async Task<ActionResult> SaveTemplates(string storeId, [FromBody] SaveFilesModel value)
        {
            await SaveFilesTo(storeId, value, "/default");
            return Ok();
        }

        private async Task SaveFilesTo(string storeId, SaveFilesModel model, string path)
        {
            var templates = JsonConvert.DeserializeObject<JObject>(model.Files).ToObject<Dictionary<string, JObject>>();
            var storageProvider = _blobContentStorageProviderFactory.CreateProvider(GetContentBasePath(storeId));
            var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };

            foreach (var templateName in templates.Keys)
            {
                var filepath = $"{path}/{templateName}";
                var content = templates[templateName];
                await using var targetStream = await storageProvider.OpenWriteAsync(filepath);
                await using var writer = new StreamWriter(targetStream);
                var stringContent = JsonConvert.SerializeObject(content, settings);
                await writer.WriteAsync(stringContent);
            }
        }

        private async Task<string> GetFilesFromFolder(string storeId, string folder)
        {
            var templatesFolder = $"/default/config/schemas/{folder}";
            var basePath = GetContentBasePath(storeId);
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

        private string GetContentBasePath(string storeId)
        {
            var result = $"Themes/{storeId}";
            return result;
        }

        public class SaveFilesModel
        {
            public string Files { get; set; }
        }
    }
}
