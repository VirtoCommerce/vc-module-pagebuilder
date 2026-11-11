using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
    ///   PATCH  /repos/{repo}/git/refs/heads/{branch}  — move one that already exists
    ///   DELETE /repos/{repo}/git/refs/heads/{branch}  — remove it once the page is published
    ///   GET    /repos/{repo}/contents/{path}?ref=     — read a page (and the file sha updates need)
    ///   PUT    /repos/{repo}/contents/{path}          — create or update it
    ///   DELETE /repos/{repo}/contents/{path}          — remove it
    /// </summary>
    public class GitHubContentRepository : IGitContentRepository
    {
        public const string HttpClientName = GitHubApiClient.HttpClientName;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly GitContentOptions _options;

        public GitHubContentRepository(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IOptions<GitContentOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _options = options.Value;
        }

        public virtual async Task<string> ReadFileAsync(string repository, string path, string gitRef, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(gitRef);

            var cacheKey = ReadCacheKey(repository, path, gitRef);
            if (_memoryCache.TryGetValue<string>(cacheKey, out var cached))
            {
                return cached;
            }

            var content = await FetchFileAsync(repository, path, gitRef, cancellationToken);

            // misses are cached too: the designer polls a page that does not exist yet on the work
            // branch on every keystroke-triggered status refresh
            CacheRead(repository, path, gitRef, content);

            return content;
        }

        public virtual async Task<string> GetBranchHeadShaAsync(string repository, string branch, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);

            var operation = $"resolve branch \"{branch}\"";
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Get, RefUrl(repository, branch), null, operation, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var body = await GitHubApiClient.ReadAsAsync<JObject>(response, operation, cancellationToken);
            return body?["object"]?["sha"]?.Value<string>();
        }

        public virtual async Task CreateBranchAsync(string repository, string branch, string fromRef, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            ArgumentException.ThrowIfNullOrWhiteSpace(fromRef);

            var sha = GitCommitSha.IsSha(fromRef)
                ? fromRef
                : await GetBranchHeadShaAsync(repository, fromRef, cancellationToken)
                  ?? throw new GitContentException($"Could not resolve \"{fromRef}\" to a commit.");

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var body = new JObject
            {
                ["ref"] = $"refs/heads/{branch}",
                ["sha"] = sha,
            };

            var operation = $"create branch \"{branch}\" from \"{fromRef}\"";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Post, $"repos/{repository}/git/refs",
                GitHubApiClient.CreateJsonContent(body), operation, cancellationToken);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                // "Reference already exists" — someone cut the same branch between our check and this
                // call, which is the outcome this method wanted anyway. Every OTHER 422 (a name git
                // refuses, an unknown sha, a repository rule) is a real failure: reporting success would
                // send the save that follows straight at a branch that does not exist.
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                if (details?.Contains("Reference already exists", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return;
                }

                throw GitHubApiClient.CreateException(response, details, operation);
            }

            await GitHubApiClient.EnsureSuccessAsync(response, operation, cancellationToken);
        }

        public virtual async Task SetBranchAsync(string repository, string branch, string sha, string pagePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            ArgumentException.ThrowIfNullOrWhiteSpace(sha);
            ArgumentException.ThrowIfNullOrWhiteSpace(pagePath);

            var client = _httpClientFactory.CreateClient(HttpClientName);

            // force: the new commit is deliberately not a descendant of the old one — that is the whole
            // point of rebuilding a draft on top of the production branch. Without it GitHub refuses
            // anything but a fast-forward.
            var body = new JObject
            {
                ["sha"] = sha,
                ["force"] = true,
            };

            var operation = $"move branch \"{branch}\" to {sha}";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Patch, MutableRefUrl(repository, branch),
                GitHubApiClient.CreateJsonContent(body), operation, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // no such branch yet: "point it here" and "cut it here" are the same request then
                await CreateBranchAsync(repository, branch, sha, cancellationToken);
            }
            else
            {
                await GitHubApiClient.EnsureSuccessAsync(response, operation, cancellationToken);
            }

            InvalidateRead(repository, pagePath, branch);
        }

        public virtual async Task DeleteBranchAsync(string repository, string branch, string pagePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            ArgumentException.ThrowIfNullOrWhiteSpace(pagePath);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var operation = $"delete branch \"{branch}\"";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Delete, MutableRefUrl(repository, branch), null, operation, cancellationToken);

            // already gone: publishing deletes the work branch, and so does the repository's
            // "automatically delete head branches" setting — whichever wins, the caller is satisfied
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                await GitHubApiClient.EnsureSuccessAsync(response, operation, cancellationToken);
            }

            // the draft is gone, so the read that would still serve it has to go with it — including the
            // cached miss, which is what a not-yet-saved page on this branch left behind
            InvalidateRead(repository, pagePath, branch);
        }

        public virtual async Task<string> CommitFileAsync(string repository, string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var escapedPath = EscapePath(path);

            // The branch already holding exactly these bytes is a save that has nothing to write: GitHub
            // answers such a PUT with 422, which reads as a stale sha, and the retry fails the same way —
            // so a no-op save or a restore of the current draft would surface as a failed write. The
            // commit that holds them is the branch head, and that is the answer.
            var bytes = PageJson.Encoding.GetBytes(content ?? string.Empty);

            var current = await GetFileAsync(client, repository, escapedPath, branch, cancellationToken);
            if (current?.Holds(bytes) == true)
            {
                return await GetBranchHeadShaAsync(repository, branch, cancellationToken);
            }

            var response = await PutContentAsync(client, repository, escapedPath, content, branch, message, author, current?.Sha, cancellationToken);

            // A stale sha (the file changed on the branch between GET and PUT) comes back as 409/422 —
            // refresh it once and retry. A branch belongs to one editor and one page, so the only writer
            // it can race with is that editor saving twice in a row.
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
            {
                response.Dispose();
                current = await GetFileAsync(client, repository, escapedPath, branch, cancellationToken);

                // the write that raced with this one may have been this very content
                if (current?.Holds(bytes) == true)
                {
                    CacheRead(repository, path, branch, content);
                    return await GetBranchHeadShaAsync(repository, branch, cancellationToken);
                }

                response = await PutContentAsync(client, repository, escapedPath, content, branch, message, author, current?.Sha, cancellationToken);
            }

            using (response)
            {
                var operation = $"commit \"{path}\" to branch \"{branch}\"";
                await GitHubApiClient.EnsureSuccessAsync(response, operation, cancellationToken);
                CacheRead(repository, path, branch, content);
                return await ReadCommitShaAsync(response, operation, cancellationToken);
            }
        }

        public virtual async Task<string> DeleteFileAsync(string repository, string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var escapedPath = EscapePath(path);

            var sha = (await GetFileAsync(client, repository, escapedPath, branch, cancellationToken))?.Sha
                      ?? throw new GitContentException($"Cannot delete \"{path}\": it does not exist on branch \"{branch}\".");

            var body = new JObject
            {
                ["message"] = message,
                ["sha"] = sha,
                ["branch"] = branch,
            };
            AddAuthor(body, author);

            var operation = $"delete \"{path}\" on branch \"{branch}\"";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Delete, $"repos/{repository}/contents/{escapedPath}",
                GitHubApiClient.CreateJsonContent(body), operation, cancellationToken);

            await GitHubApiClient.EnsureSuccessAsync(response, operation, cancellationToken);
            CacheRead(repository, path, branch, null);
            return await ReadCommitShaAsync(response, operation, cancellationToken);
        }

        private async Task<string> FetchFileAsync(string repository, string path, string gitRef, CancellationToken cancellationToken)
        {
            var operation = $"read \"{path}\" at \"{gitRef}\"";
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Get,
                $"repos/{repository}/contents/{EscapePath(path)}?ref={Uri.EscapeDataString(gitRef)}", null, operation, cancellationToken);

            // 404 covers both "no such file" and "no such ref"; neither is an error for a caller that
            // only wants to know what the page looks like there
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var body = await GitHubApiClient.ReadAsAsync<JObject>(response, operation, cancellationToken);
            var encoded = body?["content"]?.Value<string>();
            if (encoded == null)
            {
                // the contents API refuses to inline files over 1 MB; a page that large is a bug elsewhere
                throw new GitContentException($"GitHub returned no inline content for \"{path}\" at \"{gitRef}\" (too large?).");
            }

            return PageJson.Encoding.GetString(Convert.FromBase64String(encoded));
        }

        private static async Task<RemoteFile> GetFileAsync(HttpClient client, string repository, string escapedPath, string branch, CancellationToken cancellationToken)
        {
            var operation = $"read current sha of \"{escapedPath}\"";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Get,
                $"repos/{repository}/contents/{escapedPath}?ref={Uri.EscapeDataString(branch)}", null, operation, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null; // new file on this branch
            }

            var body = await GitHubApiClient.ReadAsAsync<JObject>(response, operation, cancellationToken);
            var encoded = body?["content"]?.Value<string>();

            return new RemoteFile(
                body?["sha"]?.Value<string>(),
                // not inlined above 1 MB: such a file is simply never taken for "unchanged"
                encoded == null ? null : Convert.FromBase64String(encoded));
        }

        /// <summary>A file as it stands on a branch: the sha an update must name, and its bytes when GitHub inlined them.</summary>
        private sealed record RemoteFile(string Sha, byte[] Content)
        {
            public bool Holds(byte[] bytes) => Content != null && Content.AsSpan().SequenceEqual(bytes);
        }

        private static Task<HttpResponseMessage> PutContentAsync(HttpClient client, string repository, string escapedPath, string content, string branch, string message, GitCommitAuthor author, string sha, CancellationToken cancellationToken)
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

            return GitHubApiClient.SendRequestAsync(client, HttpMethod.Put, $"repos/{repository}/contents/{escapedPath}",
                GitHubApiClient.CreateJsonContent(body), $"commit \"{escapedPath}\" to branch \"{branch}\"", cancellationToken);
        }

        private static void AddAuthor(JObject body, GitCommitAuthor author)
        {
            if (!string.IsNullOrEmpty(author?.Name) && !string.IsNullOrEmpty(author.Email))
            {
                body["author"] = new JObject { ["name"] = author.Name, ["email"] = author.Email };
            }
        }

        private static async Task<string> ReadCommitShaAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
        {
            var body = await GitHubApiClient.ReadAsAsync<JObject>(response, operation, cancellationToken);
            return body?["commit"]?["sha"]?.Value<string>();
        }

        public virtual void InvalidateRead(string repository, string path, string gitRef)
        {
            _memoryCache.Remove(ReadCacheKey(repository, path, gitRef));
        }

        // A write puts what it wrote into the read cache rather than dropping the entry: right after a
        // commit the contents API can still answer the previous version, and a read that took it would
        // keep it for as long as any other read.
        private void CacheRead(string repository, string path, string gitRef, string content)
        {
            _memoryCache.Set(ReadCacheKey(repository, path, gitRef), content, GitCommitSha.IsSha(gitRef)
                ? _options.ImmutableReadCacheExpiration
                : _options.ReadCacheExpiration);
        }

        private static string ReadCacheKey(string repository, string path, string gitRef) => $"{nameof(GitHubContentRepository)}:{repository}:{gitRef}:{path}";

        // a branch name may contain '/' (designer/john/about-us) — escape each segment, keep the separators

        /// <summary>Read endpoint: <c>git/ref/...</c> resolves a single ref.</summary>
        private static string RefUrl(string repository, string branch) => $"repos/{repository}/git/ref/heads/{EscapePath(branch)}";

        /// <summary>Write endpoint: <c>git/refs/...</c> creates and deletes them.</summary>
        private static string MutableRefUrl(string repository, string branch) => $"repos/{repository}/git/refs/heads/{EscapePath(branch)}";

        private static string EscapePath(string path)
        {
            var segments = path.Replace('\\', '/').TrimStart('/').Split('/');

            // Uri.EscapeDataString leaves dots alone, and HttpClient resolves "../" before the request
            // leaves: a dot segment would address a different endpoint than the one this url was built for
            if (segments.Any(segment => segment is "." or ".."))
            {
                throw new ArgumentException($"\"{path}\" must not contain \".\" or \"..\" segments.", nameof(path));
            }

            return string.Join("/", segments.Select(Uri.EscapeDataString));
        }
    }
}
