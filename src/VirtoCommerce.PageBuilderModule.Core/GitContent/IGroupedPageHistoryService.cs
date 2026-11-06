using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Version history of a database page in the store repository.</summary>
public interface IGroupedPageHistoryService
{
    /// <summary>Records the page content. Does nothing for a store without history.</summary>
    Task RecordAsync(GroupedPageBuilderPage group, string content, string summary, ClaimsPrincipal user, string saveId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the page content restored from the change <paramref name="sha"/>, named by <paramref name="version"/>
    /// when the editor restored a whole save.
    /// </summary>
    Task RecordRestoreAsync(GroupedPageBuilderPage group, string content, string sha, string version, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the page changes, newest first; <c>null</c> for a store without history.</summary>
    Task<GitFileHistoryResult> GetHistoryAsync(GroupedPageBuilderPage group, string after = null, int take = ModuleConstants.GitHistory.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the page content at a change; <c>null</c> when there is no such change or no history.</summary>
    Task<string> ReadContentAsync(GroupedPageBuilderPage group, string sha, CancellationToken cancellationToken = default);

    /// <summary>Gets the published page content; <c>null</c> while the page is not published.</summary>
    Task<string> GetPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default);

    /// <summary>Deletes the unpublished changes of the page and records the published content. Returns <c>false</c> for a page never published.</summary>
    Task<bool> DiscardDraftAsync(GroupedPageBuilderPage group, ClaimsPrincipal user, string saveId, CancellationToken cancellationToken = default);
}
