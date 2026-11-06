using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class PageBuilderContentPathsTests
    {
        [Theory]
        [InlineData("pages")]
        [InlineData("blogs")]
        [InlineData("themes")]
        [InlineData("Pages")]
        public void KnownContentTypes_AreAccepted(string type) => Assert.True(PageBuilderContentPaths.IsContentType(type));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("assets")]
        [InlineData("..")]
        [InlineData("pages/..")]
        public void OtherContentTypes_AreRefused(string type) => Assert.False(PageBuilderContentPaths.IsContentType(type));

        [Theory]
        [InlineData("about-us.page")]
        [InlineData("/about-us.page")]
        [InlineData("blogs/news/first.page")]
        [InlineData("about-us.page-draft")]
        [InlineData("default/config/settings_data.json")]
        public void PathsInsideTheContentFolder_AreAccepted(string path) => Assert.True(PageBuilderContentPaths.IsRelativePath(path));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("../about-us.page")]
        [InlineData("blogs/../../about-us.page")]
        [InlineData("./about-us.page")]
        [InlineData("blogs//about-us.page")]
        [InlineData("\\about-us.page")]
        [InlineData("blogs\\..\\about-us.page")]
        [InlineData("C:/Windows/win.ini")]
        [InlineData("about-us.page\0")]
        [InlineData("default/config./builder_settings.json")]
        [InlineData("default/config /builder_settings.json")]
        [InlineData(".. /about-us.page")]
        public void PathsThatLeaveTheContentFolder_AreRefused(string path) => Assert.False(PageBuilderContentPaths.IsRelativePath(path));

        [Theory]
        [InlineData("default", true)]
        [InlineData("b2b-theme", true)]
        [InlineData("default/config", false)]
        [InlineData("..", false)]
        [InlineData("", false)]
        public void ThemeIsOneFolderName(string theme, bool expected) => Assert.Equal(expected, PageBuilderContentPaths.IsFolderName(theme));

        [Theory]
        [InlineData("pages", "about-us.page", true)]
        [InlineData("blogs", "blogs/news.page", true)]
        [InlineData("themes", "default/config/settings_data.json", true)]
        [InlineData("pages", "about-us.json", false)]
        [InlineData("pages", "/about-us.page-draft", true)]
        [InlineData("blogs", "blogs/news.page-draft", true)]
        [InlineData("themes", "default/config/settings_data.json-draft", false)]
        [InlineData("pages", "about-us.html-draft", false)]
        [InlineData("themes", "default/layout.liquid", false)]
        [InlineData("assets", "x.page", false)]
        [InlineData("themes", "default/content/templates/product.json", true)]
        [InlineData("themes", "/default/config/settings_data.json", true)]
        [InlineData("themes", "default/config/builder_settings.json", false)]
        [InlineData("themes", "default/config/settings_schema.json", false)]
        [InlineData("themes", "default/config/template_rules.json", true)]
        [InlineData("themes", "default/config/brand.json", true)]
        [InlineData("themes", "default/config/palette.json", true)]
        [InlineData("themes", "default/config/palette.dark.json", true)]
        [InlineData("themes", "default/config/palette.light.json", false)]
        [InlineData("themes", "default/config/schemas/sections/banner.json", false)]
        [InlineData("themes", "default/Config/builder_settings.json", false)]
        [InlineData("themes", "default/config/schemas/settings_data.json", false)]
        [InlineData("themes", "settings_data.json", false)]
        public void OnlyTheFilesTheDesignerWrites_AreWritable(string type, string path, bool expected)
        {
            var file = new PageBuilderContentFile { ContentType = type, Path = path, Content = JToken.Parse("{\"settings\":{\"type\":\"settings\"},\"content\":[]}") };

            Assert.Equal(expected, new PageBuilderContentFileValidator().Validate(file).IsValid);
        }

        [Theory]
        [InlineData("default/templates/product.json", true)]
        [InlineData("/default/templates/product.json", true)]
        [InlineData("default/config/template_rules.json", true)]
        [InlineData("default/Config/Template_Rules.json", true)]
        [InlineData("default/config/settings_data.json", false)]
        [InlineData("default/sections/header-group.json", false)]
        [InlineData("default/content/templates/product.json", false)]
        [InlineData("templates/product.json", false)]
        [InlineData(null, false)]
        public void IsTemplateFile_ThemePath_TellsTemplatesAndRulesApart(string path, bool expected) =>
            Assert.Equal(expected, PageBuilderContentPaths.IsTemplateFile(path));
    }
}
