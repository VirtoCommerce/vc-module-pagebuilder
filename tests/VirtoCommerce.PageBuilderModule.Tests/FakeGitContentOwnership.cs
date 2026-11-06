using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Data.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// Every repository is <paramref name="storeId"/>'s (<c>null</c>: nobody's); remembers which ones were asked about, and forgotten.
/// How a description marks a repository stays the module's own.
/// </summary>
internal sealed class FakeGitContentOwnership(string storeId = null) : GitHubContentOwnership(null, null, null)
{
    public List<string> Asked { get; } = [];

    public List<string> Forgotten { get; } = [];

    public override Task<string> GetStoreIdAsync(string repository, CancellationToken cancellationToken = default)
    {
        Asked.Add(repository);
        return Task.FromResult(storeId);
    }

    public override void Invalidate(string repository) => Forgotten.Add(repository);
}
