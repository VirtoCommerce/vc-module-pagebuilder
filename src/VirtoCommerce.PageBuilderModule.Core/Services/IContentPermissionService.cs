using System.Security.Claims;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IContentPermissionService
{
    /// <summary>The path has the theme folder.</summary>
    string GetThemeFileEditPermission(string path);

    string GetEditPermission(HistoryItem item);

    Task<bool> HasPermissionsAsync(ClaimsPrincipal user, IEnumerable<string> permissions);

    Task<bool> CanAccessPageAsync(ClaimsPrincipal user, GroupedPageBuilderPage group, string storeId);
}
