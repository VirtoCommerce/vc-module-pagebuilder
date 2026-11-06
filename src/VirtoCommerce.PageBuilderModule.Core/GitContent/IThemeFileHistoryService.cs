using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Version history of a theme file in the store repository.</summary>
public interface IThemeFileHistoryService
{
    /// <summary>Records the file content. Does nothing for a store without history.</summary>
    Task RecordAsync(string storeId, string path, string content, string summary, ClaimsPrincipal user, string saveId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the file changes, newest first; the one holding <paramref name="publishedContent"/> is the published one.
    /// <c>null</c> for a store without history.
    /// </summary>
    Task<GitFileHistoryResult> GetHistoryAsync(string storeId, string path, string publishedContent, string after = null, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets the file content at a change; <c>null</c> when there is no such change or no history.</summary>
    Task<string> ReadContentAsync(string storeId, string path, string sha, CancellationToken cancellationToken = default);
}
