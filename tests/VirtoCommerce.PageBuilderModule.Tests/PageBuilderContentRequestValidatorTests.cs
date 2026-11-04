using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class PageBuilderContentRequestValidatorTests
    {
        private const string Store = "vccom";

        [Fact]
        public async Task AReadInsideTheStore_IsValid()
        {
            var result = await Validator().ValidateAsync(Request(paths: ["/about-us.page"]), TestContext.Current.CancellationToken);

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ARequestWithoutAContentType_IsInvalid()
        {
            var result = await Validator().ValidateAsync(Request(type: null), TestContext.Current.CancellationToken);

            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ASaveNamesTheContentTypePerFile()
        {
            var request = Request(type: null, files: [File("pages", "about-us.page"), File("themes", "default/config/settings_data.json")]);

            var result = await Validator().ValidateAsync(request, TestContext.Current.CancellationToken);

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ASaveOfAFileTheDesignerDoesNotWrite_IsInvalid()
        {
            var request = Request(type: null, files: [File("pages", "about-us.page"), File("themes", "default/config/builder_settings.json")]);

            var result = await Validator().ValidateAsync(request, TestContext.Current.CancellationToken);

            Assert.False(result.IsValid);
            Assert.Contains("builder_settings.json", result.Errors.Single().ErrorMessage);
        }

        [Fact]
        public async Task ASaveWithAMissingFile_IsInvalid()
        {
            var result = await Validator().ValidateAsync(Request(type: null, files: [null]), TestContext.Current.CancellationToken);

            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ATheme_IsOneFolderName()
        {
            var result = await Validator().ValidateAsync(Request(theme: "../other-store"), TestContext.Current.CancellationToken);

            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task AnUnknownStore_IsInvalid()
        {
            var result = await Validator().ValidateAsync(Request(storeId: "other-store"), TestContext.Current.CancellationToken);

            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task AMalformedRequest_DoesNotLookUpTheStore()
        {
            var storeService = new FakeStoreService(new Store { Id = Store });

            await new PageBuilderContentRequestValidator(storeService, new PageBuilderContentFileValidator()).ValidateAsync(Request(paths: ["../appsettings.json"]), TestContext.Current.CancellationToken);

            Assert.Null(storeService.LastRequestedId);
        }

        private static PageBuilderContentRequestValidator Validator() =>
            new(new FakeStoreService(new Store { Id = Store }), new PageBuilderContentFileValidator());

        private static PageBuilderContentRequest Request(string storeId = Store, string type = "pages", string theme = null,
            string[] paths = null, PageBuilderContentFile[] files = null) =>
            new()
            {
                StoreId = storeId,
                ContentType = type,
                Theme = theme,
                Paths = paths ?? [],
                Files = files,
            };

        private static PageBuilderContentFile File(string type, string path) => new() { ContentType = type, Path = path };
    }
}
