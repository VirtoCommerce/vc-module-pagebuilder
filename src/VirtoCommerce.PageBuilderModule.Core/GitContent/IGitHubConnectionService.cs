using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>The configuration wins when it is complete, else the GitHub App of the one-click setup.</summary>
public interface IGitHubConnectionService
{
    /// <summary>Never throws: a saved App that cannot be read comes back with <see cref="GitHubConnection.Error"/>.</summary>
    Task<GitHubConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

    /// <exception cref="GitContentException">GitHub refused the code, or the App belongs to another organization.</exception>
    Task<GitHubAppConnection> CreateAppAsync(string code, string organization, CancellationToken cancellationToken = default);

    GitHubAppManifest CreateAppManifest(string organization, string url, string redirectUrl, string setupUrl);
}
