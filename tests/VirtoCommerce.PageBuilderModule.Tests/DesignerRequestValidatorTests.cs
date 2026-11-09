using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.Models;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class DesignerRequestValidatorTests
{
    private const string Envelope = """{"settings":{},"content":[]}""";
    private const string Header = "default/sections/header-group.json";

    private static readonly string Version = PageBuilderContentVersion.Create(new GroupedPageBuilderPage { Id = "g1", Pages = [] }, null);

    public static TheoryData<string, DesignerSaveRequest> InvalidSaves => new()
    {
        { "nothing", new DesignerSaveRequest() },
        { "a theme file left out", new DesignerSaveRequest { ThemeFiles = [null] } },
        { "a theme file named twice", new DesignerSaveRequest { ThemeFiles = [File(Header), File(Header.ToUpperInvariant())] } },
        { "a theme file without a path", new DesignerSaveRequest { ThemeFiles = [File("")] } },
        { "a page by group and path", Save(new DesignerSavePage { GroupId = "g1", Path = "home.page", Type = "pages", Content = Envelope }) },
        { "a page by neither", Save(new DesignerSavePage { Content = Envelope }) },
        { "a group id that is a path", Save(new DesignerSavePage { GroupId = "../g1", Content = Envelope }) },
        { "a file page of the theme", Save(new DesignerSavePage { Path = "header.json", Type = "themes", Content = Envelope }) },
        { "a database page without content", Save(new DesignerSavePage { GroupId = "g1" }) },
        { "a database page outside its envelope", Save(new DesignerSavePage { GroupId = "g1", Content = """{"content":[]}""" }) },
        { "a page version no read returns", Save(new DesignerSavePage { GroupId = "g1", Content = Envelope, ETag = "\"x\"" }) },
        { "a theme file version no read returns", new DesignerSaveRequest { ThemeFiles = [File(Header, "\"x\"")] } },
    };

    public static TheoryData<string, DesignerSaveRequest> ValidSaves => new()
    {
        { "a database page", Save(new DesignerSavePage { GroupId = "g1", Content = Envelope, ETag = $" {Version} " }) },
        // its version is required where the save is made (428)
        { "a database page without its version", Save(new DesignerSavePage { GroupId = "g1", Content = Envelope }) },
        { "a file page", Save(new DesignerSavePage { Path = "home.page", Type = "pages", Content = "{}" }) },
        { "theme files", new DesignerSaveRequest { ThemeFiles = [File(Header, Version), File("default/templates/product.json")] } },
    };

    [Theory]
    [MemberData(nameof(InvalidSaves))]
    public async Task DesignerSave_Invalid_IsRefused(string because, DesignerSaveRequest request)
    {
        var result = await RequestValidators.DesignerSave.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid, because);
    }

    [Theory]
    [MemberData(nameof(ValidSaves))]
    public async Task DesignerSave_Valid_IsAccepted(string because, DesignerSaveRequest request)
    {
        var result = await RequestValidators.DesignerSave.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid, $"{because}: {result}");
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("g1", "home.page", "pages")]
    [InlineData("../g1", null, null)]
    [InlineData(null, "header.json", "themes")]
    public async Task DesignerDiscard_PageNamedWrong_IsRefused(string groupId, string path, string type)
    {
        var request = new DesignerDiscardRequest { Page = new DesignerSavePage { GroupId = groupId, Path = path, Type = type } };

        Assert.False((await RequestValidators.DesignerDiscard.ValidateAsync(request, TestContext.Current.CancellationToken)).IsValid);
    }

    [Fact]
    public async Task DesignerDiscard_ThemeFilesNamedOnce_IsAccepted()
    {
        var once = new DesignerDiscardRequest { ThemeFiles = [Header, "default/templates/product.json"] };
        var twice = new DesignerDiscardRequest { ThemeFiles = [Header, Header] };

        Assert.True((await RequestValidators.DesignerDiscard.ValidateAsync(once, TestContext.Current.CancellationToken)).IsValid);
        Assert.False((await RequestValidators.DesignerDiscard.ValidateAsync(twice, TestContext.Current.CancellationToken)).IsValid);
        Assert.False((await RequestValidators.DesignerDiscard.ValidateAsync(new DesignerDiscardRequest(), TestContext.Current.CancellationToken)).IsValid);
    }

    private static DesignerSaveRequest Save(DesignerSavePage page) => new() { Page = page };

    private static DesignerSaveFile File(string path, string eTag = null) => new() { Path = path, Content = "{}", ETag = eTag };
}
