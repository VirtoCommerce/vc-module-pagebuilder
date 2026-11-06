using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// Publishes by pull request:
    ///   POST /repos/{repo}/pulls        — open one (422 when there is nothing to merge, or one is open)
    ///   GET  /repos/{repo}/pulls?head=  — find the open one to reuse
    ///   GET  /repos/{repo}/pulls/{n}    — merged? mergeable?
    ///   POST /graphql                   — enablePullRequestAutoMerge
    ///   PUT  /repos/{repo}/pulls/{n}/merge — when the repository has no auto-merge to wait for
    /// <para>
    /// Auto-merge is a GraphQL-only mutation, and it is refused on a pull request that has nothing left
    /// to wait for ("clean status"). That refusal is the signal to merge outright — which is what
    /// happens when the production branch requires no checks.
    /// </para>
    /// <para>
    /// Where the repository has auto-merge switched off AND the production branch does require checks,
    /// neither path can finish the publish: the mutation is refused and the immediate merge is blocked.
    /// That is <see cref="GitPublishState.AwaitingMerge"/> — the pull request is open and waiting for
    /// someone to come back to it, which the builder offers as publishing the page again.
    /// </para>
    /// </summary>
    public class GitHubContentPublisher : IGitContentPublisher
    {
        public const string HttpClientName = GitHubContentRepository.HttpClientName;

        private const string MergeMethod = "SQUASH";
        private const int MergeabilityAttempts = 3;
        private static readonly TimeSpan MergeabilityDelay = TimeSpan.FromMilliseconds(300);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGitHubConnectionService _gitHubConnectionService;
        private readonly GitContentOptions _options;

        public GitHubContentPublisher(IHttpClientFactory httpClientFactory, IGitHubConnectionService gitHubConnectionService, IOptions<GitContentOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _gitHubConnectionService = gitHubConnectionService;
            _options = options.Value;
        }

        public virtual async Task<GitPublishResult> MergeBranchAsync(string repository, string branch, string title, CancellationToken cancellationToken = default)
        {
            var connection = await _gitHubConnectionService.GetConnectionAsync(cancellationToken);
            return await MergeBranchIntoAsync(repository, branch, title, connection.BaseBranch, cancellationToken);
        }

        // Promotion differs from publishing in the target branch and in nothing else: the same pull
        // request, the same auto-merge, the same refusal to merge past a failing check.
        public virtual async Task<GitPublishResult> MergeBranchIntoAsync(string repository, string branch, string title, string baseBranch,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);
            ArgumentException.ThrowIfNullOrWhiteSpace(baseBranch);

            var target = baseBranch;
            var client = _httpClientFactory.CreateClient(HttpClientName);

            var pullRequest = await OpenOrReusePullRequestAsync(client, repository, branch, title, target, cancellationToken);
            if (pullRequest == null)
            {
                // nothing on the branch that the production branch does not already have
                return new GitPublishResult { State = GitPublishState.AlreadyPublished };
            }

            var number = pullRequest["number"]!.Value<int>();
            var url = pullRequest["html_url"]?.Value<string>();
            var nodeId = pullRequest["node_id"]?.Value<string>();

            var state = await MergeAsync(client, repository, number, nodeId, cancellationToken);

            return new GitPublishResult { State = state, PullRequestNumber = number, Url = url };
        }

        public virtual async Task<GitPendingPublish> GetOpenPullRequestAsync(string repository, string branch, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentException.ThrowIfNullOrWhiteSpace(branch);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var pullRequest = await FindOpenPullRequestAsync(client, repository, branch, cancellationToken);
            var number = pullRequest?["number"]?.Value<int>();

            return number == null
                ? null
                : new GitPendingPublish { Number = number.Value, AutoMerging = IsAutoMerging(pullRequest) };
        }

        // GitHub reports an armed auto-merge as an object on the pull request, and null when there is
        // none — which is the difference between "this will ship by itself" and "this is waiting for
        // somebody".
        private static bool IsAutoMerging(JObject pullRequest) =>
            pullRequest["auto_merge"] is { Type: not JTokenType.Null };

        private static async Task<JObject> OpenOrReusePullRequestAsync(HttpClient client, string repository, string branch, string title,
            string baseBranch, CancellationToken cancellationToken)
        {
            var body = new JObject
            {
                ["title"] = title,
                ["head"] = branch,
                ["base"] = baseBranch,
            };

            var operation = $"open a pull request from \"{branch}\"";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Post, $"repos/{repository}/pulls",
                GitHubApiClient.CreateJsonContent(body), operation, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await GitHubApiClient.ReadAsAsync<JObject>(response, operation, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                // GitHub answers 422 both to "there is nothing to merge" and to "you already have a pull
                // request for this branch" — the two outcomes this method knows how to live with. It also
                // answers 422 when the base or head is wrong, or a repository rule refuses the pull
                // request; those must NOT end up as AlreadyPublished, which is what the caller reports
                // when this returns null. Telling an editor their page is live when nothing was even
                // opened is the one failure this whole flow cannot afford.
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!IsNothingToMerge(details) && !IsPullRequestAlreadyOpen(details))
                {
                    throw GitHubApiClient.CreateException(response, details, operation);
                }
            }
            else
            {
                await GitHubApiClient.EnsureSuccessAsync(response, operation, cancellationToken);
            }

            return await FindOpenPullRequestAsync(client, repository, branch, cancellationToken);
        }

        // "No commits between master and designer/john/foo" — the branch holds nothing the production
        // branch does not already have, so the page is published as far as the editor is concerned.
        private static bool IsNothingToMerge(string details) =>
            details?.Contains("No commits between", StringComparison.OrdinalIgnoreCase) == true;

        // "A pull request already exists for owner:designer/john/foo." — publish was pressed twice, or an
        // earlier one armed auto-merge and is still waiting for its checks.
        private static bool IsPullRequestAlreadyOpen(string details) =>
            details?.Contains("pull request already exists", StringComparison.OrdinalIgnoreCase) == true;

        private static async Task<JObject> FindOpenPullRequestAsync(HttpClient client, string repository, string branch, CancellationToken cancellationToken)
        {
            var (owner, _) = GitHubApiClient.ParseRepository(repository);
            var head = Uri.EscapeDataString($"{owner}:{branch}");

            var operation = $"look for an open pull request from \"{branch}\"";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Get, $"repos/{repository}/pulls?state=open&head={head}", null,
                operation, cancellationToken);

            var found = await GitHubApiClient.ReadAsAsync<JArray>(response, operation, cancellationToken);
            return found?.FirstOrDefault() as JObject;
        }

        private async Task<GitPublishState> MergeAsync(HttpClient client, string repository, int number, string nodeId, CancellationToken cancellationToken)
        {
            var pullRequest = await AwaitMergeabilityAsync(client, repository, number, cancellationToken);

            if (pullRequest["merged"]?.Value<bool>() == true)
            {
                return GitPublishState.Merged;
            }

            if (pullRequest["mergeable"]?.Type == JTokenType.Boolean && !pullRequest["mergeable"]!.Value<bool>())
            {
                return GitPublishState.Conflict;
            }

            if (!string.IsNullOrEmpty(nodeId) && await TryEnableAutoMergeAsync(client, repository, nodeId, cancellationToken))
            {
                return GitPublishState.Pending;
            }

            return await TryMergeNowAsync(client, repository, number, cancellationToken);
        }

        /// <summary>
        /// GitHub computes mergeability in the background and reports <c>null</c> until it is done. Arming
        /// auto-merge on a pull request that turns out to conflict would leave it waiting forever, with
        /// the editor told their page is on its way, so it is worth a few hundred milliseconds to know.
        /// Gives up and lets the caller proceed rather than failing the publish.
        /// </summary>
        private static async Task<JObject> AwaitMergeabilityAsync(HttpClient client, string repository, int number, CancellationToken cancellationToken)
        {
            JObject pullRequest = null;

            for (var attempt = 0; attempt < MergeabilityAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(MergeabilityDelay, cancellationToken);
                }

                pullRequest = await GetPullRequestAsync(client, repository, number, cancellationToken);
                if (pullRequest["merged"]?.Value<bool>() == true || pullRequest["mergeable"]?.Type == JTokenType.Boolean)
                {
                    break;
                }
            }

            return pullRequest;
        }

        private static async Task<JObject> GetPullRequestAsync(HttpClient client, string repository, int number, CancellationToken cancellationToken)
        {
            var operation = $"read pull request #{number}";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Get, $"repos/{repository}/pulls/{number}", null, operation,
                cancellationToken);
            return await GitHubApiClient.ReadAsAsync<JObject>(response, operation, cancellationToken);
        }

        /// <summary>
        /// False when GitHub declines to arm auto-merge — the repository has it switched off, or the
        /// pull request is already clean and there is nothing to wait for. Both mean "merge it yourself".
        /// </summary>
        private async Task<bool> TryEnableAutoMergeAsync(HttpClient client, string repository, string nodeId, CancellationToken cancellationToken)
        {
            var query = new JObject
            {
                ["query"] = "mutation($pullRequestId: ID!, $mergeMethod: PullRequestMergeMethod!) { " +
                            "enablePullRequestAutoMerge(input: { pullRequestId: $pullRequestId, mergeMethod: $mergeMethod }) { clientMutationId } }",
                ["variables"] = new JObject
                {
                    ["pullRequestId"] = nodeId,
                    ["mergeMethod"] = MergeMethod,
                },
            };

            const string operation = "enable auto-merge";
            using var request = GitHubApiClient.CreateGraphQlRequest(_options.GraphQlUrl, GitHubApiClient.ParseRepository(repository).Owner, query);
            using var response = await GitHubApiClient.SendRequestAsync(client, request, operation, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            // GraphQL reports failures inside a 200 response
            var body = await GitHubApiClient.ReadAsAsync<JObject>(response, operation, cancellationToken);
            return body?["errors"] == null;
        }

        private static async Task<GitPublishState> TryMergeNowAsync(HttpClient client, string repository, int number, CancellationToken cancellationToken)
        {
            var body = new JObject { ["merge_method"] = MergeMethod.ToLowerInvariant() };

            var operation = $"merge pull request #{number}";
            using var response = await GitHubApiClient.SendRequestAsync(client, HttpMethod.Put, $"repos/{repository}/pulls/{number}/merge",
                GitHubApiClient.CreateJsonContent(body), operation, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return GitPublishState.Merged;
            }

            // 405 "not mergeable" and 409 "head changed" both mean the merge did not happen and the pull
            // request is where a human can see why. Saying "published" here would be a lie — but so is
            // "on its way" when the refusal was a conflict, and 405 covers both that and a check that has
            // not passed yet. GitHub had to finish computing mergeability to refuse at all, so the answer
            // it would not give while we waited for it is available now: ask once more, and tell an
            // editor whose page is stuck to fix it rather than leaving them watching "Publishing…".
            //
            // Anything short of a conflict is AwaitingMerge rather than Pending, because this method is
            // only reached when auto-merge could not be armed: the repository does not allow it, so no
            // check going green will merge the pull request afterwards. Pending would promise a merge
            // that nothing is going to perform.
            if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.Conflict)
            {
                var pullRequest = await GetPullRequestAsync(client, repository, number, cancellationToken);

                return pullRequest["mergeable"]?.Type == JTokenType.Boolean && !pullRequest["mergeable"]!.Value<bool>()
                    ? GitPublishState.Conflict
                    : GitPublishState.AwaitingMerge;
            }

            await GitHubApiClient.EnsureSuccessAsync(response, operation, cancellationToken);
            return GitPublishState.AwaitingMerge;
        }
    }
}
