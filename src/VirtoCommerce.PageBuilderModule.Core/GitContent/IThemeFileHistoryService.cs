using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Version history of a theme file ("default/templates/product.json") in the store repository.</summary>
public interface IThemeFileHistoryService
{
    /// <summary>Records the file content. Does nothing for a store without history.</summary>
    Task RecordAsync(string storeId, string path, string content, string summary, ClaimsPrincipal user, string saveId, CancellationToken cancellationToken = default);

    /// <summary>Gets the file changes, newest first, the published one marked; <c>null</c> for a store without history.</summary>
    Task<GitFileHistoryResult> GetHistoryAsync(string storeId, string path, string after = null, int take = ModuleConstants.GitHistory.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the file content at a change; <c>null</c> when there is no such change or no history.</summary>
    Task<string> ReadContentAsync(string storeId, string path, string sha, CancellationToken cancellationToken = default);

    /// <summary>Gets the published file content; <c>null</c> when the file is not published.</summary>
    Task<string> GetPublishedContentAsync(string storeId, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the content of the change <paramref name="sha"/> the file's draft and records it, named by <paramref name="version"/>
    /// when the editor restored a whole save. Returns <c>false</c> when there is no such change or no history.
    /// </summary>
    Task<bool> RestoreAsync(string storeId, string path, string sha, string version, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default);
}
