using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security;
using PlatformPermissions = VirtoCommerce.Platform.Core.PlatformConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// MVC checks [Authorize] before the action runs, so a test calling the action never sees it: the attributes are asserted here.
/// The read permissions can be scoped to stores, which [Authorize] does not see, so the actions that read check them themselves.
/// </summary>
public class EndpointPermissionsTests
{
    private const string Anonymous = "anonymous";

    private const string InTheAction = "in the action";

    private static readonly Type[] Controllers =
    [
        typeof(GitContentSetupController),
        typeof(HistoryController),
        typeof(MultilanguagePageController),
        typeof(PageBuilderAssetsController),
        typeof(PageBuilderPageController),
        typeof(PageBuilderSharedComponentsController),
        typeof(ThemeController),
    ];

    private static readonly Dictionary<(Type Controller, string Action), string> Expected = new()
    {
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.GetSite))] = InTheAction,
        // store:update is checked in the action because its scope can limit it to the store.
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.ConnectSite))] = InTheAction,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.DisconnectSite))] = InTheAction,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.GetConnection))] = InTheAction,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.StartAppSetup))] = PlatformPermissions.SettingUpdate,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.AppNew))] = Anonymous,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.AppCreated))] = Anonymous,
        [(typeof(GitContentSetupController), nameof(GitContentSetupController.AppInstalled))] = Anonymous,
        [(typeof(HistoryController), nameof(HistoryController.GetStatus))] = InTheAction,
        [(typeof(HistoryController), nameof(HistoryController.GetHistory))] = InTheAction,
        [(typeof(HistoryController), nameof(HistoryController.GetVersion))] = InTheAction,
        [(typeof(HistoryController), nameof(HistoryController.Restore))] = Permissions.Update,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Search))] = InTheAction,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Get))] = InTheAction,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Create))] = Permissions.Create,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Update))] = Permissions.Update,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Delete))] = Permissions.Delete,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.GetLanguages))] = InTheAction,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.CreateLanguage))] = Permissions.Create,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Link))] = Permissions.Update,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Convert))] = $"{Permissions.Create} + {Permissions.Update}",
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.MarkUpToDate))] = Permissions.Update,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Unlink))] = Permissions.Update,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.ResetOverrides))] = Permissions.Update,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.Clone))] = Permissions.Create,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.PublishAll))] = Permissions.Publish,
        [(typeof(MultilanguagePageController), nameof(MultilanguagePageController.ArchiveAll))] = Permissions.Delete,
        [(typeof(PageBuilderAssetsController), nameof(PageBuilderAssetsController.Search))] = PlatformPermissions.AssetRead,
        [(typeof(PageBuilderAssetsController), nameof(PageBuilderAssetsController.SearchReferences))] = InTheAction,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.SearchGroups))] = InTheAction,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.SearchList))] = InTheAction,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.FindPermalinkUsage))] = InTheAction,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.GetCopyName))] = InTheAction,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.GetGroup))] = InTheAction,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.UpdateGroup))] = Permissions.Update,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.CreateGroup))] = Permissions.Create,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.CreateGroupPage))] = Permissions.Create,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.ArchiveGroups))] = Permissions.Delete,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.PublishGroup))] = Permissions.Publish,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.DiscardGroupDraft))] = Permissions.Update,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.DeleteGroup))] = Permissions.Delete,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.PublishStatus))] = InTheAction,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.GetPageContent))] = InTheAction,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.SavePageContent))] = Permissions.Update,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.SavePageContentJson))] = Permissions.Update,
        [(typeof(PageBuilderPageController), nameof(PageBuilderPageController.CopyPageContent))] = Permissions.Update,
        [(typeof(PageBuilderSharedComponentsController), nameof(PageBuilderSharedComponentsController.Search))] = InTheAction,
        [(typeof(PageBuilderSharedComponentsController), nameof(PageBuilderSharedComponentsController.Get))] = InTheAction,
        [(typeof(PageBuilderSharedComponentsController), nameof(PageBuilderSharedComponentsController.Create))] = Permissions.SharedComponentsCreate,
        [(typeof(PageBuilderSharedComponentsController), nameof(PageBuilderSharedComponentsController.Update))] = Permissions.SharedComponentsUpdate,
        [(typeof(PageBuilderSharedComponentsController), nameof(PageBuilderSharedComponentsController.Delete))] = Permissions.SharedComponentsDelete,
        [(typeof(PageBuilderSharedComponentsController), nameof(PageBuilderSharedComponentsController.GetContent))] = InTheAction,
        [(typeof(PageBuilderSharedComponentsController), nameof(PageBuilderSharedComponentsController.SaveContent))] = Permissions.SharedComponentsUpdate,
        [(typeof(ThemeController), nameof(ThemeController.Publish))] = Permissions.Publish,
        [(typeof(ThemeController), nameof(ThemeController.GetStatus))] = InTheAction,
        [(typeof(ThemeController), nameof(ThemeController.SaveCatalog))] = Permissions.Update,
        [(typeof(ThemeController), nameof(ThemeController.GetCatalog))] = InTheAction,
        [(typeof(ThemeController), nameof(ThemeController.GeneratePalette))] = Permissions.Theme,
        [(typeof(ThemeController), nameof(ThemeController.CheckPalette))] = Permissions.Theme,
    };

    private static readonly HashSet<(Type Controller, string Action)> AnonymousCallbacks =
    [
        (typeof(GitContentSetupController), nameof(GitContentSetupController.AppNew)),
        (typeof(GitContentSetupController), nameof(GitContentSetupController.AppCreated)),
        (typeof(GitContentSetupController), nameof(GitContentSetupController.AppInstalled)),
    ];

    public static TheoryData<Type, string, string> ListedActions()
    {
        var actions = new TheoryData<Type, string, string>();
        foreach (var ((controller, action), access) in Expected)
        {
            actions.Add(controller, action, access);
        }
        return actions;
    }

    [Theory]
    [MemberData(nameof(ListedActions))]
    public void Action_Listed_RequiresItsPermission(Type controller, string action, string access)
    {
        var method = ActionsOf(controller).SingleOrDefault(x => x.Name == action);

        Assert.NotNull(method);
        Assert.Equal(access, AccessOf(method));
    }

    [Fact]
    public void Controllers_EveryAction_IsListed()
    {
        var actions = Controllers.SelectMany(controller => ActionsOf(controller).Select(method => (controller, method.Name)));

        Assert.All(actions, action => Assert.Contains(action, Expected.Keys));
    }

    [Fact]
    public void Controllers_OnlyTheGitHubAppCallbacks_AreOpenToAnonymousCallers()
    {
        Assert.All(Controllers, controller => Assert.Empty(controller.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>()));

        var anonymous = Controllers.SelectMany(controller => ActionsOf(controller)
            .Where(method => method.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any())
            .Select(method => (controller, method.Name)));
        Assert.Equal(AnonymousCallbacks.Order(), anonymous.Order());
    }

    // MVC routes a public method that has no HTTP method attribute under the controller's route, so those count too.
    private static IEnumerable<MethodInfo> ActionsOf(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && !method.IsDefined(typeof(NonActionAttribute), inherit: true));

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
