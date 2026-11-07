using System.Security.Claims;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

/// <summary>Drafts of pages and blog articles kept as files: the editor's work branch on the git flow, else a draft blob.</summary>
public interface IFilePageDraftService
{
    Task SaveDraftAsync(string storeId, PageBuilderContentFile page, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Commits the page to the editor's work branch, cut from the base branch at the first save.</summary>
    Task<GitPageCommit> CommitDraftAsync(string storeId, string repository, PageBuilderContentFile page, string action, ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<DraftDiscardStatus> DiscardDraftAsync(string storeId, string contentType, string path, ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the editor's work branch of the page.</summary>
    Task DiscardGitDraftAsync(string repository, string contentType, string path, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Deletes the draft blob of the page; refused when nothing is published, as the draft is then the only copy.</summary>
    Task<DraftDiscardStatus> DiscardBlobDraftAsync(string storeId, string contentType, string path, string theme,
        CancellationToken cancellationToken = default);
}
