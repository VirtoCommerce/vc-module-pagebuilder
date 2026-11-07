using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using Xunit;
using Permissions = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class ContentPermissionServiceTests
{
    [Theory]
    [InlineData("default/templates/product.json", Permissions.Templates)]
    [InlineData("/default/templates/product.json", Permissions.Templates)]
    [InlineData("default/config/template_rules.json", Permissions.Templates)]
    [InlineData("default/Config/Template_Rules.json", Permissions.Templates)]
    [InlineData("default/config/settings_data.json", Permissions.Theme)]
    [InlineData("default/sections/header-group.json", Permissions.Theme)]
    [InlineData("default/content/templates/product.json", Permissions.Theme)]
    [InlineData("templates/product.json", Permissions.Theme)]
    [InlineData(null, Permissions.Theme)]
    public void GetThemeFileEditPermission_ThemePath_TellsTemplatesAndRulesApart(string path, string expected) =>
        Assert.Equal(expected, new ContentPermissionService(new FakeAuthorizationService(true)).GetThemeFileEditPermission(path));

    [Fact]
    public void GetEditPermission_Page_IsUpdate() =>
        Assert.Equal(Permissions.Update, new ContentPermissionService(new FakeAuthorizationService(true)).GetEditPermission(new HistoryItem(HistoryItemType.Page, "g1")));

    [Fact]
    public void GetThemeFileEditPermission_OverriddenRule_DecidesThePermission() =>
        Assert.Equal(Permissions.Templates, new SectionsAreTemplatesPermissionService().GetThemeFileEditPermission("default/sections/header-group.json"));

    [Fact]
    public async Task HasPermissionsAsync_OneMissing_ReturnsFalse()
    {
        var service = new ContentPermissionService(new FakeAuthorizationService("s1", Permissions.Read, Permissions.Theme));

        Assert.True(await service.HasPermissionsAsync(null, [Permissions.Theme, Permissions.Theme]));
        Assert.False(await service.HasPermissionsAsync(null, [Permissions.Theme, Permissions.Templates]));
    }

    [Fact]
    public async Task CanAccessPageAsync_PageOfAnotherStore_ReturnsFalse()
    {
        var service = new ContentPermissionService(new FakeAuthorizationService(true));

        Assert.True(await service.CanAccessPageAsync(null, new GroupedPageBuilderPage { Id = "g1", StoreId = "S1" }, "s1"));
        Assert.False(await service.CanAccessPageAsync(null, new GroupedPageBuilderPage { Id = "g1", StoreId = "s2" }, "s1"));
    }

    private sealed class SectionsAreTemplatesPermissionService() : ContentPermissionService(new FakeAuthorizationService(true))
    {
        protected override bool IsTemplateFile(string path) => base.IsTemplateFile(path) || path.Contains("/sections/");
    }
}
