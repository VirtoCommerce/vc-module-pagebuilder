using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>GitHub connection: the configuration when it is complete, else the GitHub App of the one-click setup.</summary>
public interface IGitHubConnectionService
{
    /// <summary>
    /// Gets the connection in effect. Never throws: a saved App that cannot be read comes back with <see cref="GitHubConnection.Error"/>,
    /// and <see cref="IGitContentPolicy"/> then refuses git operations for the stores that opted in.
    /// </summary>
    Task<GitHubConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Converts the App manifest code into the created GitHub App and saves it.</summary>
    /// <exception cref="GitContentException">GitHub refused the code, or the App belongs to another organization.</exception>
    Task<GitHubAppConnection> CreateAppAsync(string code, string organization, CancellationToken cancellationToken = default);

    /// <summary>Creates the manifest of the GitHub App the one-click setup creates.</summary>
    GitHubAppManifest CreateAppManifest(string organization, string url, string redirectUrl, string setupUrl);
}
