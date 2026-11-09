using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public interface IGitContentOwnership
{
    Task<string> GetStoreIdAsync(string repository, CancellationToken cancellationToken = default);

    string GetStoreId(string description);

    string GetDescription(string storeId);

    void EnsureOwnedBy(string repository, string owner, string storeId);

    void Invalidate(string repository);
}
