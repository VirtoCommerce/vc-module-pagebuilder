using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>GitHub REST and GraphQL API. Every failure is a <see cref="GitContentException"/>.</summary>
public interface IGitHubApiClient
{
    /// <summary>Gets the repository; <c>null</c> when it does not exist.</summary>
    Task<GitHubRepositoryInfo> GetRepositoryAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>Creates the repository in its organization; <c>null</c> when it already exists.</summary>
    Task<GitHubRepositoryInfo> CreateRepositoryAsync(string repository, JObject settings, CancellationToken cancellationToken = default);

    /// <summary>Updates the repository settings.</summary>
    Task UpdateRepositoryAsync(string repository, JObject settings, CancellationToken cancellationToken = default);

    /// <summary>Checks that the organization exists and the platform has access to it.</summary>
    Task<bool> HasOrganizationAsync(string organization, CancellationToken cancellationToken = default);

    /// <summary>Posts a GraphQL request on behalf of the owner.</summary>
    Task<JObject> PostGraphQlAsync(Uri url, string owner, JObject request, string operation, CancellationToken cancellationToken = default);

    /// <summary>Gets the tree of the commit.</summary>
    Task<string> GetCommitTreeAsync(string repository, string commitSha, CancellationToken cancellationToken = default);

    /// <summary>Creates a tree: the base tree with the files, keyed by path, written over it. Returns its sha.</summary>
    Task<string> CreateTreeAsync(string repository, string baseTreeSha, IDictionary<string, string> files, CancellationToken cancellationToken = default);

    /// <summary>Creates a commit of the tree on top of the parent. Returns its sha.</summary>
    Task<string> CreateCommitAsync(string repository, string treeSha, string parentSha, string message, GitCommitAuthor author,
        CancellationToken cancellationToken = default);

    /// <summary>Moves the branch to the commit; <c>false</c> when that is not a fast-forward, as the branch moved meanwhile.</summary>
    Task<bool> FastForwardBranchAsync(string repository, string branch, string commitSha, CancellationToken cancellationToken = default);

    /// <summary>Sends a request without credentials.</summary>
    Task<HttpResponseMessage> SendAnonymousAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken = default);
}
