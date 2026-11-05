using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>
/// IGitContentOwnership over GitHub: the description the module gives every repository it creates or adopts names
/// the store after <see cref="Marker"/>. A store's mark stays, so it is kept for
/// <see cref="GitContentOptions.ImmutableReadCacheExpiration"/>; "nobody's" lasts until the module adopts the
/// repository, on any server, so it is kept for <see cref="GitContentOptions.ReadCacheExpiration"/> only.
/// </summary>
public class GitHubContentOwnership(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IOptions<GitContentOptions> options)
    : IGitContentOwnership
{
    /// <summary>The start of the description of every repository the module creates; the store id follows.</summary>
    public const string Marker = "Page Builder content of store ";

    public virtual async Task<string> GetStoreIdAsync(string repository, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var cacheKey = CacheKey(repository);
        if (memoryCache.TryGetValue<string>(cacheKey, out var cached))
        {
            return cached;
        }

        var client = httpClientFactory.CreateClient(GitHubContentRepository.HttpClientName);
        using var response = await client.GetAsync($"repos/{repository}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // not made yet: it may be made for a store any moment, so this is not kept
            return null;
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw GitHubContentRepository.Failed(response, details, $"read repository \"{repository}\"");
        }

        var storeId = StoreOf(JObject.Parse(details));
        memoryCache.Set(cacheKey, storeId, storeId == null ? options.Value.ReadCacheExpiration : options.Value.ImmutableReadCacheExpiration);
        return storeId;
    }

    public virtual void Invalidate(string repository) => memoryCache.Remove(CacheKey(repository));

    /// <summary>
    /// Refuses a repository the module created for another store: two stores whose ids name one repository
    /// ("B2B Store", "b2b-store"), or a setting naming another store's, would publish over each other's pages.
    /// </summary>
    public static void EnsureOwnedBy(string repository, string owner, string storeId)
    {
        if (owner != null && !string.Equals(owner, storeId, StringComparison.Ordinal))
        {
            throw new GitRepositoryOwnershipException(repository, owner);
        }
    }

    // GitHub names a repository the same whatever the case of its letters
    private static string CacheKey(string repository) => $"{nameof(GitHubContentOwnership)}:{repository.ToLowerInvariant()}";

    /// <summary>The store a repository's description names; <c>null</c> for one the module did not create.</summary>
    public static string StoreOf(JObject repository)
    {
        var description = repository["description"]?.Value<string>();
        return description?.StartsWith(Marker, StringComparison.Ordinal) == true ? description[Marker.Length..].Trim() : null;
    }
}
