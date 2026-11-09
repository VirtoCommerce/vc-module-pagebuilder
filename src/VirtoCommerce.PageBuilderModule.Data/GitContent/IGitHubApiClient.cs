using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>Every failure is a <see cref="GitContentException"/>.</summary>
public interface IGitHubApiClient
{
    Task<GitHubRepositoryInfo> GetRepositoryAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>null</c> when it already exists.</summary>
    Task<GitHubRepositoryInfo> CreateRepositoryAsync(string repository, JObject settings, CancellationToken cancellationToken = default);

    Task UpdateRepositoryAsync(string repository, JObject settings, CancellationToken cancellationToken = default);

    Task<bool> HasOrganizationAsync(string organization, CancellationToken cancellationToken = default);

    Task<JObject> PostGraphQlAsync(Uri url, string owner, JObject request, string operation, CancellationToken cancellationToken = default);

    Task<string> GetCommitTreeAsync(string repository, string commitSha, CancellationToken cancellationToken = default);

    Task<string> CreateTreeAsync(string repository, string baseTreeSha, IDictionary<string, string> files, CancellationToken cancellationToken = default);

    Task<string> CreateCommitAsync(string repository, string treeSha, string parentSha, string message, GitCommitAuthor author,
        CancellationToken cancellationToken = default);

    /// <summary>Returns <c>false</c> when that is not a fast-forward, as the branch moved meanwhile.</summary>
    Task<bool> FastForwardBranchAsync(string repository, string branch, string commitSha, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> SendAnonymousAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken = default);
}
