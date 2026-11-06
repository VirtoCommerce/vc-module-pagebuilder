using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security;
using PlatformPermissions = VirtoCommerce.Platform.Core.PlatformConstants.Security.Permissions;
using StorePermissions = VirtoCommerce.StoreModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// What the site, theme and history endpoints require before they run. MVC checks the [Authorize] and
/// [AllowAnonymous] attributes outside the action, so a test that calls the action does not see them.
/// </summary>
public class EndpointPermissionsTests
{
    private const string Anonymous = "anonymous";

    // the permission depends on the request, so the action asks for it
    private const string InTheAction = "in the action";

    private static readonly Type[] Controllers =
    [
        typeof(GitContentSetupController),
        typeof(GroupedPageHistoryController),
        typeof(ThemeController),
        typeof(ThemeFileHistoryController),
    ];

    private static readonly Dictionary<(Type Controller, string Action), string> Expected = new()
    {
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.GetSite))] = Permissions.Read,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.ConnectSite))] = StorePermissions.Update,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.DisconnectSite))] = StorePermissions.Update,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.GetConnection))] = Permissions.Read,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.StartAppSetup))] = PlatformPermissions.SettingUpdate,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.AppNew))] = Anonymous,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.AppCreated))] = Anonymous,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.AppInstalled))] = Anonymous,
        [(typeof(GroupedPageHistoryController), nameof(GroupedPageHistoryController.GetHistory))] = Permissions.Read,
        [(typeof(GroupedPageHistoryController), nameof(GroupedPageHistoryController.GetVersion))] = Permissions.Read,
        [(typeof(GroupedPageHistoryController), nameof(GroupedPageHistoryController.Restore))] = Permissions.Update,
        [(typeof(GroupedPageHistoryController), nameof(GroupedPageHistoryController.Discard))] = Permissions.Update,
        [(typeof(ThemeController), nameof(ThemeController.Publish))] = Permissions.Publish,
        [(typeof(ThemeController), nameof(ThemeController.SaveCatalog))] = Permissions.Theme,
        [(typeof(ThemeController), nameof(ThemeController.GetCatalog))] = Permissions.Read,
        [(typeof(ThemeFileHistoryController), nameof(ThemeFileHistoryController.GetHistory))] = Permissions.Read,
        [(typeof(ThemeFileHistoryController), nameof(ThemeFileHistoryController.GetVersion))] = Permissions.Read,
        [(typeof(ThemeFileHistoryController), nameof(ThemeFileHistoryController.Restore))] = InTheAction,
    };

    public static TheoryData<Type, string, string> Actions()
    {
        var actions = new TheoryData<Type, string, string>();
        foreach (var ((controller, action), access) in Expected)
        {
            actions.Add(controller, action, access);
        }
        return actions;
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public void An_action_requires_its_permission(Type controller, string action, string access)
    {
        var method = controller.GetMethod(action);

        Assert.NotNull(method);
        Assert.Equal(access, AccessOf(method));
    }

    [Fact]
    public void Every_action_of_these_controllers_is_listed()
    {
        // an action added without a row here would have no permission asserted
        var actions = Controllers.SelectMany(controller => controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(method => (controller, method.Name)));

        Assert.All(actions, action => Assert.Contains(action, Expected.Keys));
    }

    private static string AccessOf(MethodInfo method)
    {
        if (method.GetCustomAttribute<AllowAnonymousAttribute>() != null)
        {
            return Anonymous;
        }

        var policies = method.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).Where(x => x != null).ToList();
        return policies.Count == 0 ? InTheAction : string.Join(" + ", policies);
    }
}
