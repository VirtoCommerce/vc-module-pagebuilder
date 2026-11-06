using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// MVC checks [Authorize] before the action runs, so a test calling the action never sees it: the attributes are asserted here.
    /// </summary>
    public class PageBuilderControllerPermissionsTests
    {
        private static readonly Dictionary<string, string[]> Expected = new()
        {
            { nameof(PageBuilderController.GetTemplate), [Permissions.Read] },
            { nameof(PageBuilderController.GetSettings), [Permissions.Read] },
            { nameof(PageBuilderController.GetTemplates), [Permissions.Read] },
            { nameof(PageBuilderController.GetObjects), [Permissions.Read] },
            { nameof(PageBuilderController.GetSectionsSettings), [Permissions.Read] },
            { nameof(PageBuilderController.GetSchemasCatalog), [Permissions.Read] },
            { nameof(PageBuilderController.GetSchemaByKey), [Permissions.Read] },
            { nameof(PageBuilderController.Search), [Permissions.Read] },
            { nameof(PageBuilderController.SaveTemplates), [Permissions.Update] },
            // and the permission that edits each part, in the action
            { nameof(PageBuilderController.SaveDesignerChanges), [Permissions.Read] },
            { nameof(PageBuilderController.GitPublish), [Permissions.Publish] },
            { nameof(PageBuilderController.GitPromote), [Permissions.Promote] },
            { nameof(PageBuilderController.GitUnpublish), [Permissions.Publish] },
            { nameof(PageBuilderController.GitPublishStatus), [Permissions.Read] },
            { nameof(PageBuilderController.GitHistory), [Permissions.Read] },
            { nameof(PageBuilderController.GitRestoreVersion), [Permissions.Update] },
            { nameof(PageBuilderController.GitPreview), [Permissions.Read] },
            { nameof(PageBuilderController.DiscardDraft), [Permissions.Update] },
            { nameof(PageBuilderController.GitDiscardDraft), [Permissions.Update] },
            { nameof(PageBuilderController.GetLegacyDrafts), [Permissions.Read] },
            { nameof(PageBuilderController.DeleteLegacyDrafts), [Permissions.Delete] },
            { nameof(PageBuilderController.GitCopyPage), [Permissions.Create] },
            { nameof(PageBuilderController.GetLegacyDraft), [Permissions.Read] },
        };

        public static TheoryData<string, string[]> ActionPermissions =>
            new(Expected.Select(row => new TheoryDataRow<string, string[]>(row.Key, row.Value)));

        [Theory]
        [MemberData(nameof(ActionPermissions))]
        public void Action_RequiresItsPermissions(string action, string[] permissions)
        {
            var method = typeof(PageBuilderController).GetMethod(action);

            Assert.NotNull(method);
            Assert.Equal(permissions.Order(), PoliciesOf(method).Order());
        }

        [Fact]
        public void EveryAction_IsListedAbove()
        {
            Assert.All(Actions().Select(method => method.Name), action => Assert.Contains(action, Expected.Keys));
        }

        [Fact]
        public void NoAction_IsOpenToAnonymousCallers()
        {
            Assert.Empty(typeof(PageBuilderController).GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>());
            Assert.All(Actions(), action => Assert.Empty(action.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>()));
        }

        // Everything MVC routes, HTTP method attribute or not: a public method without a route of its own takes the controller's.
        private static IEnumerable<MethodInfo> Actions() =>
            typeof(PageBuilderController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && !method.IsDefined(typeof(NonActionAttribute), inherit: true));

        private static string[] PoliciesOf(MethodInfo method) =>
            method.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).Where(policy => policy != null).ToArray();
    }
}
