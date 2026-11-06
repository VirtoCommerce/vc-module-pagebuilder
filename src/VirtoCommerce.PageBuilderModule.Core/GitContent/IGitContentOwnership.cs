using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Store ownership of module-created repositories.</summary>
public interface IGitContentOwnership
{
    /// <summary>Gets the id of the store the repository was created for; <c>null</c> if none.</summary>
    Task<string> GetStoreIdAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>Gets the id of the store a repository description names; <c>null</c> if none.</summary>
    string GetStoreId(string description);

    /// <summary>Gets the description that marks a repository as the store's.</summary>
    string GetDescription(string storeId);

    /// <summary>Throws when the repository was created for another store.</summary>
    /// <exception cref="GitRepositoryOwnershipException"><paramref name="owner"/> is another store.</exception>
    void EnsureOwnedBy(string repository, string owner, string storeId);

    /// <summary>Clears the cached owner of the repository.</summary>
    void Invalidate(string repository);
}
