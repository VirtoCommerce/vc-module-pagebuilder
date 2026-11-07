using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>Reads repository ownership from the GitHub repository description.</summary>
public class GitHubContentOwnership(IGitHubApiClient gitHubApiClient, IMemoryCache memoryCache, IOptions<GitContentOptions> options)
    : IGitContentOwnership
{
    /// <summary>Description prefix of module-created repositories.</summary>
    public const string DescriptionPrefix = "Page Builder content of store ";

    public virtual async Task<string> GetStoreIdAsync(string repository, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var cacheKey = GetCacheKey(repository);
        if (memoryCache.TryGetValue<string>(cacheKey, out var cached))
        {
            return cached;
        }

        var repositoryInfo = await gitHubApiClient.GetRepositoryAsync(repository, cancellationToken);
        if (repositoryInfo == null)
        {
            // not cached: the repository may be created any moment
            return null;
        }

        var storeId = GetStoreId(repositoryInfo.Description);
        memoryCache.Set(cacheKey, storeId, storeId == null ? options.Value.ReadCacheExpiration : options.Value.ImmutableReadCacheExpiration);
        return storeId;
    }

    public virtual string GetStoreId(string description) =>
        description?.StartsWith(DescriptionPrefix, StringComparison.Ordinal) == true ? description[DescriptionPrefix.Length..].Trim() : null;

    public virtual string GetDescription(string storeId) => DescriptionPrefix + storeId;

    public virtual void EnsureOwnedBy(string repository, string owner, string storeId)
    {
        if (owner != null && !string.Equals(owner, storeId, StringComparison.Ordinal))
        {
            throw new GitRepositoryOwnershipException(repository, owner);
        }
    }

    public virtual void Invalidate(string repository) => memoryCache.Remove(GetCacheKey(repository));

    // GitHub repository names are case-insensitive
    protected virtual string GetCacheKey(string repository) => $"{nameof(GitHubContentOwnership)}:{repository.ToLowerInvariant()}";
}
