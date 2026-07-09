using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// IGitContentWriter over the GitHub REST API:
    ///   GET  /repos/{repo}/git/ref/heads/{branch}      — does the work branch exist
    ///   POST /repos/{repo}/git/refs                    — fork it from the base branch when not
    ///   GET  /repos/{repo}/contents/{path}?ref=        — current file sha (needed for updates)
    ///   PUT  /repos/{repo}/contents/{path}             — create/update the file on the branch
    /// One editor = one branch (see GitContentOptions.BranchTemplate), so concurrent saves of the
    /// same file race only with the editor themselves; a stale sha (409/422) is retried once with
    /// a fresh one.
    /// </summary>
    public class GitHubContentWriter : IGitContentWriter
    {
        public const string HttpClientName = "PageBuilderGitContent";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GitContentOptions _options;

        public GitHubContentWriter(IHttpClientFactory httpClientFactory, IOptions<GitContentOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task CommitFileAsync(string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            if (string.IsNullOrWhiteSpace(_options.Repository))
            {
                throw new InvalidOperationException($"Git content is not configured: set {GitContentOptions.SectionName}:Repository (\"owner/name\").");
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);

            await EnsureBranchExistsAsync(client, branch, cancellationToken);

            var escapedPath = EscapePath(path.Replace('\\', '/').TrimStart('/'));
            var sha = await GetFileShaAsync(client, escapedPath, branch, cancellationToken);
            var response = await PutContentAsync(client, escapedPath, content, branch, message, author, sha, cancellationToken);

            // A stale sha (the file changed on the branch between GET and PUT) comes back as
            // 409/422 — refresh the sha once and retry; per-editor branches make longer races unrealistic.
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
            {
                response.Dispose();
                sha = await GetFileShaAsync(client, escapedPath, branch, cancellationToken);
                response = await PutContentAsync(client, escapedPath, content, branch, message, author, sha, cancellationToken);
            }

            using (response)
            {
                await ThrowIfFailedAsync(response, $"commit \"{path}\" to branch \"{branch}\"");
            }
        }

        private async Task EnsureBranchExistsAsync(HttpClient client, string branch, CancellationToken cancellationToken)
        {
            using var branchResponse = await client.GetAsync(RefUrl(branch), cancellationToken);
            if (branchResponse.StatusCode != HttpStatusCode.NotFound)
            {
                await ThrowIfFailedAsync(branchResponse, $"check branch \"{branch}\"");
                return;
            }

            using var baseResponse = await client.GetAsync(RefUrl(_options.BaseBranch), cancellationToken);
            await ThrowIfFailedAsync(baseResponse, $"resolve base branch \"{_options.BaseBranch}\"");
            var baseSha = JObject.Parse(await baseResponse.Content.ReadAsStringAsync(cancellationToken))["object"]?["sha"]?.Value<string>();
            if (string.IsNullOrEmpty(baseSha))
            {
                throw new InvalidOperationException($"Could not resolve the sha of base branch \"{_options.BaseBranch}\".");
            }

            var body = new JObject
            {
                ["ref"] = $"refs/heads/{branch}",
                ["sha"] = baseSha,
            };
            using var createResponse = await client.PostAsync(
                $"repos/{_options.Repository}/git/refs",
                JsonContent(body),
                cancellationToken);

            // 422 "Reference already exists" — someone created the branch between our GET and POST; fine.
            if (createResponse.StatusCode != HttpStatusCode.UnprocessableEntity)
            {
                await ThrowIfFailedAsync(createResponse, $"create branch \"{branch}\" from \"{_options.BaseBranch}\"");
            }
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

        private async Task<HttpResponseMessage> PutContentAsync(HttpClient client, string escapedPath, string content, string branch, string message, GitCommitAuthor author, string sha, CancellationToken cancellationToken)
        {
            var body = new JObject
            {
                ["message"] = message,
                ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
                ["branch"] = branch,
            };
            if (!string.IsNullOrEmpty(sha))
            {
                body["sha"] = sha;
            }
            if (!string.IsNullOrEmpty(author?.Name) && !string.IsNullOrEmpty(author.Email))
            {
                body["author"] = new JObject { ["name"] = author.Name, ["email"] = author.Email };
            }

            return await client.PutAsync($"repos/{_options.Repository}/contents/{escapedPath}", JsonContent(body), cancellationToken);
        }

        private string RefUrl(string branch)
        {
            // a branch name may contain '/' (designer/john) — escape each segment, keep the separators
            return $"repos/{_options.Repository}/git/ref/heads/{EscapePath(branch)}";
        }

        private static string EscapePath(string path)
        {
            return string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
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
