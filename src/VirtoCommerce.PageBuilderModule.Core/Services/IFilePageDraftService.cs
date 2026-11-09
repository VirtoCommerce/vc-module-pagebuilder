using System.Security.Claims;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IFilePageDraftService
{
    Task SaveDraftAsync(string storeId, PageBuilderContentFile page, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    Task<GitPageCommit> CommitDraftAsync(string storeId, string repository, PageBuilderContentFile page, string action, ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<DraftDiscardStatus> DiscardDraftAsync(string storeId, string contentType, string path, ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task DiscardGitDraftAsync(string repository, string contentType, string path, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Refused when nothing is published, as the draft is then the only copy.</summary>
    Task<DraftDiscardStatus> DiscardBlobDraftAsync(string storeId, string contentType, string path, string theme,
        CancellationToken cancellationToken = default);
}
