using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Whose a repository is: the module marks every repository it creates with the store it creates it for, so that
    /// two stores never work in one repository.
    /// </summary>
    public interface IGitContentOwnership
    {
        /// <summary>
        /// The store the module created <paramref name="repository"/> for; <c>null</c> for a repository it did not
        /// create, or one that does not exist.
        /// </summary>
        Task<string> GetStoreIdAsync(string repository, CancellationToken cancellationToken = default);

        /// <summary>Forgets what is known of the repository's owner: called after the module marks it as a store's.</summary>
        void Invalidate(string repository);
    }
}
