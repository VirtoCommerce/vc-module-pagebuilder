using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// The permission every <see cref="PageBuilderController"/> action needs is on its [Authorize]
    /// attributes, which MVC checks before the action runs: a unit test calling the action does not see
    /// them, so they are asserted here. Several attributes on one action all have to pass.
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
            { nameof(PageBuilderController.GitPublish), [Permissions.Read, Permissions.Publish] },
            { nameof(PageBuilderController.GitPromote), [Permissions.Read, Permissions.Promote] },
            { nameof(PageBuilderController.GitUnpublish), [Permissions.Read, Permissions.Publish] },
            { nameof(PageBuilderController.GitPublishStatus), [Permissions.Read] },
            { nameof(PageBuilderController.GitHistory), [Permissions.Read] },
            { nameof(PageBuilderController.GitRestoreVersion), [Permissions.Update] },
            { nameof(PageBuilderController.GitPreview), [Permissions.Read] },
            { nameof(PageBuilderController.GitDiscardDraft), [Permissions.Update] },
            { nameof(PageBuilderController.GetLegacyDrafts), [Permissions.Read] },
            { nameof(PageBuilderController.DeleteLegacyDrafts), [Permissions.Read, Permissions.Delete] },
            { nameof(PageBuilderController.GitCopyPage), [Permissions.Read, Permissions.Create] },
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
            // an action added without a row here would have no permission asserted
            var actions = typeof(PageBuilderController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(method => method.Name);

            Assert.All(actions, action => Assert.Contains(action, Expected.Keys));
        }

        private static string[] PoliciesOf(MethodInfo method) =>
            method.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).Where(policy => policy != null).ToArray();
    }
}
