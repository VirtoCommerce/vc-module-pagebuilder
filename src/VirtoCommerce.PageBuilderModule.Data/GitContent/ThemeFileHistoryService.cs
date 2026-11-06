using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>Version history of a theme file in the store repository.</summary>
public class ThemeFileHistoryService(
    IGitContentPolicy gitContentPolicy,
    IGitContentRepository gitContentRepository,
    IGitHubApiClient gitHubApiClient,
    IGitHubConnectionService gitHubConnectionService,
    IOptions<GitContentOptions> options,
    ILogger<ThemeFileHistoryService> logger)
    : GitHubFileHistoryService(gitContentPolicy, gitContentRepository, gitHubApiClient, gitHubConnectionService, options, logger), IThemeFileHistoryService
{
    public const string Folder = "theme";

    /// <summary>Checks for a relative theme path without "." or ".." segments.</summary>
    public static bool IsThemePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && !path.Contains('\\') && path.Split('/').All(x => x.Length > 0 && x != "." && x != "..");

    public virtual Task RecordAsync(string storeId, string path, string content, string summary, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default)
    {
        return IsThemePath(path)
            ? RecordFileAsync(storeId, GetFilePath(path), GetCommitSubject(path), content, summary, user, saveId, cancellationToken)
            : Task.CompletedTask;
    }

    public virtual Task<GitFileHistoryResult> GetHistoryAsync(string storeId, string path, string publishedContent, string after = null, int take = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return GetFileHistoryAsync(storeId, GetFilePath(path), GetCommitSubject(path), publishedContent, after, take, cancellationToken);
    }

    public virtual Task<string> ReadContentAsync(string storeId, string path, string sha, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return ReadFileAsync(storeId, GetFilePath(path), sha, cancellationToken);
    }

    protected virtual string GetFilePath(string path) => $"{Folder}/{path}";

    protected virtual string GetCommitSubject(string path) => $": {path}";
}
