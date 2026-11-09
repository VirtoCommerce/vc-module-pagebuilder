using VirtoCommerce.PageBuilderModule.Data.Theme;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class ThemeStorageTests
{
    [Theory]
    [InlineData("product.json", "product")]
    [InlineData("product.b2c.json", "product.b2c")]
    [InlineData("page.big-sale.json", "page.big-sale")]
    [InlineData("blog.json", null)]
    [InlineData("product.B2C.json", null)]
    [InlineData("product.json-draft", null)]
    public void GetTemplateKey_FileName_ReturnsKey(string fileName, string key)
    {
        Assert.Equal(key, ThemeStorage.GetTemplateKey(fileName));
    }
}
