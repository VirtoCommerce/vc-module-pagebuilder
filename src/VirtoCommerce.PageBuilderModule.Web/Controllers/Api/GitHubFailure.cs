using System;
using System.Net.Http;
using System.Threading;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

/// <summary>
/// GitHub not doing what was asked — an error, a refusal, no answer in time — which a request reports to the caller.
/// The caller giving up is not one, and neither is a repository of another store's: RepositoryOwnershipConflict
/// answers that.
/// </summary>
internal static class GitHubFailure
{
    public static bool Is(Exception failure, CancellationToken requestAborted) =>
        !requestAborted.IsCancellationRequested
        && failure is HttpRequestException or OperationCanceledException or (InvalidOperationException and not GitRepositoryOwnershipException);
}
