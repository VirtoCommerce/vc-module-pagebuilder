using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>Each bound of the history, palette and App setup request rules, refused just past it and accepted at it.</summary>
public class RequestValidatorRulesTests
{
    private const string Page = "page:g1";

    // a commit sha in base64url
    private const string Version = "AAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task History_Take_IsBetweenOneAndAHundred(int take, bool valid)
    {
        Assert.Equal(valid, await IsValidAsync(new ContentHistoryRequest { StoreId = "s1", Items = [Page], Take = take }));
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public async Task History_Items_AreAtMostTwenty(int count, bool valid)
    {
        var items = Enumerable.Range(0, count).Select(i => $"page:g{i}").ToList();

        Assert.Equal(valid, await IsValidAsync(new ContentHistoryRequest { StoreId = "s1", Items = items }));
    }

    [Theory]
    [InlineData("abc_DEF-123", true)]
    [InlineData("abc+def", false)]
    [InlineData("abc=", false)]
    [InlineData("", false)]
    public async Task History_Cursor_IsBase64Url(string cursor, bool valid)
    {
        Assert.Equal(valid, await IsValidAsync(new ContentHistoryRequest { StoreId = "s1", Items = [Page], Cursor = cursor }));
    }

    [Fact]
    public async Task History_Version_NamesOnePageAtMost()
    {
        Assert.False(await IsValidAsync(new ContentHistoryRequest { StoreId = "s1", Items = [Page, "page:g2"], VersionId = Version }));
        Assert.True(await IsValidAsync(new ContentHistoryRequest { StoreId = "s1", Items = [Page, "theme:default/templates/product.json"], VersionId = Version }));
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAA", true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAA", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAA+", false)]
    public async Task History_VersionId_IsACommitShaInBase64Url(string versionId, bool valid)
    {
        Assert.Equal(valid, await IsValidAsync(new ContentHistoryRequest { StoreId = "s1", Items = [Page], VersionId = versionId }));
    }

    [Theory]
    [InlineData("abc12345", true)]
    [InlineData("abc1234", false)]
    [InlineData("../x1234567", false)]
    public async Task Store_CatalogHash_IsTheCatalogsFileName(string catalogHash, bool valid)
    {
        var result = await RequestValidators.Stores("s1").ValidateAsync(new PageBuilderStoreRequest { StoreId = "s1", CatalogHash = catalogHash }, TestContext.Current.CancellationToken);

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("color_primary_500", true)]
    [InlineData("Color_primary_500", false)]
    [InlineData("color primary", false)]
    public async Task PaletteTokens_Name_IsLowerCaseLettersDigitsAndUnderscores(string token, bool valid)
    {
        Assert.Equal(valid, (await new ThemePaletteTokensValidator().ValidateAsync(new ThemePaletteTokens { Light = [token], Dark = [] }, TestContext.Current.CancellationToken)).IsValid);
    }

    [Fact]
    public async Task PaletteTokens_Bounds_AreRefusedPastThem()
    {
        var validator = new ThemePaletteTokensValidator();
        var ct = TestContext.Current.CancellationToken;
        var tooMany = Enumerable.Range(0, ThemePaletteTokensValidator.MaxTokens + 1).Select(i => $"color_t_{i}").ToList();

        Assert.False((await validator.ValidateAsync(new ThemePaletteTokens { Light = tooMany, Dark = [] }, ct)).IsValid);
        Assert.False((await validator.ValidateAsync(new ThemePaletteTokens { Light = [new string('a', 101)], Dark = [] }, ct)).IsValid);
        Assert.True((await validator.ValidateAsync(new ThemePaletteTokens { Light = [new string('a', 100)], Dark = [] }, ct)).IsValid);
        Assert.False((await validator.ValidateAsync(new ThemePaletteTokens { Light = [], Dark = null }, ct)).IsValid);
    }

    [Fact]
    public async Task PaletteGeneration_Secondary_IsAColorWhenSent()
    {
        var validator = new ThemePaletteGenerationRequestValidator(new ThemePaletteTokensValidator());
        var ct = TestContext.Current.CancellationToken;
        ThemePaletteGenerationRequest Request(string secondary) =>
            new() { Primary = "#336699", Secondary = secondary, Tokens = new ThemePaletteTokens { Light = ["color_primary_500"], Dark = [] } };

        Assert.True((await validator.ValidateAsync(Request(null), ct)).IsValid);
        Assert.True((await validator.ValidateAsync(Request("#abc"), ct)).IsValid);
        Assert.False((await validator.ValidateAsync(Request("teal"), ct)).IsValid);
    }

    [Theory]
    [InlineData("acme", true)]
    [InlineData("a23456789012345678901234567890123456789", true)]
    [InlineData("a234567890123456789012345678901234567890", false)]
    [InlineData("-acme", false)]
    public async Task AppSetup_Organization_IsAGitHubLogin(string organization, bool valid)
    {
        var result = await RequestValidators.AppSetup.ValidateAsync(new GitHubAppSetupRequest { Organization = organization }, TestContext.Current.CancellationToken);

        Assert.Equal(valid, result.IsValid);
    }

    private static async Task<bool> IsValidAsync(ContentHistoryRequest request) =>
        (await RequestValidators.History("s1").ValidateAsync(request, TestContext.Current.CancellationToken)).IsValid;
}
