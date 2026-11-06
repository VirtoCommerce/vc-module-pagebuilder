using System;
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

    /// <summary>Sends a request without credentials.</summary>
    Task<HttpResponseMessage> SendAnonymousAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken = default);
}
