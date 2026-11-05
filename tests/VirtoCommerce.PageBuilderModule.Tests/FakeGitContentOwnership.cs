using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>Every repository is <paramref name="storeId"/>'s (<c>null</c>: nobody's); remembers which ones were asked about, and forgotten.</summary>
internal sealed class FakeGitContentOwnership(string storeId = null) : IGitContentOwnership
{
    public List<string> Asked { get; } = [];

    public List<string> Forgotten { get; } = [];

    public Task<string> GetStoreIdAsync(string repository, CancellationToken cancellationToken = default)
    {
        Asked.Add(repository);
        return Task.FromResult(storeId);
    }

    public void Invalidate(string repository) => Forgotten.Add(repository);
}
