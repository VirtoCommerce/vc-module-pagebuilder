using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// The version history of a theme file — a template, the template rules, a section group, the theme settings: every
    /// save, restore and discard is also committed to the site's repository, on its base branch, as "theme/{path}"
    /// ("theme/default/templates/product.json"). Blob storage stays the file's source of truth (see
    /// <see cref="SiteHistory"/>); publishing moves the saved draft live byte for byte, so the change it publishes is
    /// already there and is told by its content.
    /// </summary>
    public class ThemeFileHistory(
        IGitContentPolicy policy,
        IGitContentRepository repository,
        IHttpClientFactory httpClientFactory,
        IOptions<GitContentOptions> options,
        ILogger<ThemeFileHistory> logger)
        : SiteHistory(policy, repository, httpClientFactory, options, logger)
    {
        public const string Folder = "theme";

        /// <summary>
        /// A theme file's path as the theme storage names it ("default/templates/product.json"): relative, no "." or
        /// ".." segments, so its place in the repository is always under <see cref="Folder"/>.
        /// </summary>
        public static bool IsThemePath(string path) =>
            !string.IsNullOrWhiteSpace(path) && !path.Contains('\\') && path.Split('/').All(x => x.Length > 0 && x != "." && x != "..");

        public static string PathOf(string path) => $"{Folder}/{path}";

        /// <summary>Commits the file's content as it now stands; does nothing for a store without version history.</summary>
        public virtual Task RecordAsync(string storeId, string path, string content, string summary, ClaimsPrincipal user, string save, CancellationToken cancellationToken = default)
        {
            return IsThemePath(path) ? RecordFileAsync(storeId, PathOf(path), About(path), content, summary, user, save, cancellationToken) : Task.CompletedTask;
        }

        /// <summary>
        /// The file's changes, newest first; the one holding <paramref name="publishedContent"/> (the live file) is the
        /// published one. <c>null</c> for a store without version history.
        /// </summary>
        public virtual Task<GroupedPageHistoryResult> GetAsync(string storeId, string path, string publishedContent, string after = null, int take = 20, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return GetFileHistoryAsync(storeId, PathOf(path), About(path), publishedContent, after, take, cancellationToken);
        }

        /// <summary>The file's content at a change; <c>null</c> when there is no such change or no history.</summary>
        public virtual Task<string> ReadAsync(string storeId, string path, string sha, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return ReadFileAsync(storeId, PathOf(path), sha, cancellationToken);
        }

        private static string About(string path) => $": {path}";
    }
}
