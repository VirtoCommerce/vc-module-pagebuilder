using System.Security.Claims;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

/// <summary>Decides what a user may edit in the builder.</summary>
public interface IContentPermissionService
{
    /// <summary>Gets the permission that edits the theme file, by its path with the theme folder.</summary>
    string GetThemeFileEditPermission(string path);

    /// <summary>Gets the permission that edits the page or the theme file.</summary>
    string GetEditPermission(HistoryItem item);

    Task<bool> HasPermissionsAsync(ClaimsPrincipal user, IEnumerable<string> permissions);

    /// <summary>Checks the user's access to the database page and that it belongs to the store.</summary>
    Task<bool> CanAccessPageAsync(ClaimsPrincipal user, GroupedPageBuilderPage group, string storeId);
}
