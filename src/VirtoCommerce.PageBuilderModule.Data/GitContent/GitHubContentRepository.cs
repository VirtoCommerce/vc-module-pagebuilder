using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// IGitContentRepository over the GitHub REST API:
    ///   GET    /repos/{repo}/git/ref/heads/{branch}   — where a branch points
    ///   POST   /repos/{repo}/git/refs                 — cut a branch
    ///   DELETE /repos/{repo}/git/refs/heads/{branch}  — remove it once the page is published
    ///   GET    /repos/{repo}/contents/{path}?ref=     — read a page (and the file sha updates need)
    ///   PUT    /repos/{repo}/contents/{path}          — create or update it
    ///   DELETE /repos/{repo}/contents/{path}          — remove it
    /// </summary>
    public class GitHubContentRepository : IGitContentRepository
    {
        public const string HttpClientName = "PageBuilderGitContent";

        // a commit sha, as opposed to a branch or tag name — an immutable ref, so its reads cache long
        private static readonly Regex CommitSha = new("^[0-9a-f]{40}$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly GitContentOptions _options;

        public GitHubContentRepository(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IOptions<GitContentOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _options = options.Value;
        }

        public async Task<string> ReadFileAsync(string path, string gitRef, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(gitRef);
            EnsureConfigured();

            var cacheKey = ReadCacheKey(path, gitRef);
            if (_memoryCache.TryGetValue<string>(cacheKey, out var cached))
            {
                return cached;
            }

            var content = await FetchFileAsync(path, gitRef, cancellationToken);

            // misses are cached too: the designer polls a page that does not exist yet on the work
            // branch on every keystroke-triggered status refresh
            _memoryCache.Set(cacheKey, content, CommitSha.IsMatch(gitRef)
                ? _options.ImmutableReadCacheExpiration
                : _options.ReadCacheExpiration);

            return content;
        }

        public async Task<string> GetBranchHeadShaAsync(string branch, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            EnsureConfigured();

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(RefUrl(branch), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await ThrowIfFailedAsync(response, $"resolve branch \"{branch}\"");
            var body = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return body["object"]?["sha"]?.Value<string>();
        }

        public async Task CreateBranchAsync(string branch, string fromRef, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            ArgumentException.ThrowIfNullOrWhiteSpace(fromRef);
            EnsureConfigured();

            var sha = CommitSha.IsMatch(fromRef)
                ? fromRef
                : await GetBranchHeadShaAsync(fromRef, cancellationToken)
                  ?? throw new InvalidOperationException($"Could not resolve \"{fromRef}\" to a commit.");

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var body = new JObject
            {
                ["ref"] = $"refs/heads/{branch}",
                ["sha"] = sha,
            };

            using var response = await client.PostAsync($"repos/{_options.Repository}/git/refs", JsonContent(body), cancellationToken);

            // 422 "Reference already exists" — someone cut the same branch between our check and this call
            if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
            {
                await ThrowIfFailedAsync(response, $"create branch \"{branch}\" from \"{fromRef}\"");
            }
        }

        public async Task DeleteBranchAsync(string branch, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            EnsureConfigured();

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.DeleteAsync(MutableRefUrl(branch), cancellationToken);

            // already gone: publishing deletes the work branch, and so does the repository's
            // "automatically delete head branches" setting — whichever wins, the caller is satisfied
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                await ThrowIfFailedAsync(response, $"delete branch \"{branch}\"");
            }
        }

        public async Task<string> CommitFileAsync(string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            EnsureConfigured();

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var escapedPath = EscapePath(path);

            var sha = await GetFileShaAsync(client, escapedPath, branch, cancellationToken);
            var response = await PutContentAsync(client, escapedPath, content, branch, message, author, sha, cancellationToken);

            // A stale sha (the file changed on the branch between GET and PUT) comes back as 409/422 —
            // refresh it once and retry. A branch belongs to one editor and one page, so the only writer
            // it can race with is that editor saving twice in a row.
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
            {
                response.Dispose();
                sha = await GetFileShaAsync(client, escapedPath, branch, cancellationToken);
                response = await PutContentAsync(client, escapedPath, content, branch, message, author, sha, cancellationToken);
            }

            using (response)
            {
                await ThrowIfFailedAsync(response, $"commit \"{path}\" to branch \"{branch}\"");
                InvalidateRead(path, branch);
                return await ReadCommitShaAsync(response, cancellationToken);
            }
        }

        public async Task<string> DeleteFileAsync(string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            EnsureConfigured();

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var escapedPath = EscapePath(path);

            var sha = await GetFileShaAsync(client, escapedPath, branch, cancellationToken)
                      ?? throw new InvalidOperationException($"Cannot delete \"{path}\": it does not exist on branch \"{branch}\".");

            var body = new JObject
            {
                ["message"] = message,
                ["sha"] = sha,
                ["branch"] = branch,
            };
            AddAuthor(body, author);

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"repos/{_options.Repository}/contents/{escapedPath}")
            {
                Content = JsonContent(body),
            };
            using var response = await client.SendAsync(request, cancellationToken);

            await ThrowIfFailedAsync(response, $"delete \"{path}\" on branch \"{branch}\"");
            InvalidateRead(path, branch);
            return await ReadCommitShaAsync(response, cancellationToken);
        }

        private async Task<string> FetchFileAsync(string path, string gitRef, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(
                $"repos/{_options.Repository}/contents/{EscapePath(path)}?ref={Uri.EscapeDataString(gitRef)}",
                cancellationToken);

            // 404 covers both "no such file" and "no such ref"; neither is an error for a caller that
            // only wants to know what the page looks like there
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await ThrowIfFailedAsync(response, $"read \"{path}\" at \"{gitRef}\"");

            var body = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var encoded = body["content"]?.Value<string>();
            if (encoded == null)
            {
                // the contents API refuses to inline files over 1 MB; a page that large is a bug elsewhere
                throw new InvalidOperationException($"GitHub returned no inline content for \"{path}\" at \"{gitRef}\" (too large?).");
            }

            return PageJson.Encoding.GetString(Convert.FromBase64String(encoded));
        }

        private async Task<string> GetFileShaAsync(HttpClient client, string escapedPath, string branch, CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync(
                $"repos/{_options.Repository}/contents/{escapedPath}?ref={Uri.EscapeDataString(branch)}",
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null; // new file on this branch
            }

            await ThrowIfFailedAsync(response, $"read current sha of \"{escapedPath}\"");
            return JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken))["sha"]?.Value<string>();
        }

        private Task<HttpResponseMessage> PutContentAsync(HttpClient client, string escapedPath, string content, string branch, string message, GitCommitAuthor author, string sha, CancellationToken cancellationToken)
        {
            var body = new JObject
            {
                ["message"] = message,
                ["content"] = Convert.ToBase64String(PageJson.Encoding.GetBytes(content)),
                ["branch"] = branch,
            };
            if (!string.IsNullOrEmpty(sha))
            {
                body["sha"] = sha;
            }
            AddAuthor(body, author);

            return client.PutAsync($"repos/{_options.Repository}/contents/{escapedPath}", JsonContent(body), cancellationToken);
        }

        private static void AddAuthor(JObject body, GitCommitAuthor author)
        {
            if (!string.IsNullOrEmpty(author?.Name) && !string.IsNullOrEmpty(author.Email))
            {
                body["author"] = new JObject { ["name"] = author.Name, ["email"] = author.Email };
            }
        }

        private static async Task<string> ReadCommitShaAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var body = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return body["commit"]?["sha"]?.Value<string>();
        }

        private void InvalidateRead(string path, string branch)
        {
            _memoryCache.Remove(ReadCacheKey(path, branch));
        }

        private string ReadCacheKey(string path, string gitRef) => $"{nameof(GitHubContentRepository)}:{_options.Repository}:{gitRef}:{path}";

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_options.Repository))
            {
                throw new InvalidOperationException($"Git content is not configured: set {GitContentOptions.SectionName}:Repository (\"owner/name\").");
            }
        }

        // a branch name may contain '/' (designer/john/about-us) — escape each segment, keep the separators

        /// <summary>Read endpoint: <c>git/ref/...</c> resolves a single ref.</summary>
        private string RefUrl(string branch) => $"repos/{_options.Repository}/git/ref/heads/{EscapePath(branch)}";

        /// <summary>Write endpoint: <c>git/refs/...</c> creates and deletes them.</summary>
        private string MutableRefUrl(string branch) => $"repos/{_options.Repository}/git/refs/heads/{EscapePath(branch)}";

        private static string EscapePath(string path)
        {
            return string.Join("/", path.Replace('\\', '/').TrimStart('/').Split('/').Select(Uri.EscapeDataString));
        }

        private static StringContent JsonContent(JObject body)
        {
            return new StringContent(body.ToString(), Encoding.UTF8, "application/json");
        }

        private static async Task ThrowIfFailedAsync(HttpResponseMessage response, string operation)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var details = await response.Content.ReadAsStringAsync();
            if (details?.Length > 500)
            {
                details = details[..500];
            }

            throw new HttpRequestException($"GitHub request to {operation} failed: {(int)response.StatusCode} {response.ReasonPhrase}. {details}");
        }
    }
}
