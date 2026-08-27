using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class GitHubContentPublisherTests
    {
        private const string Branch = "designer/john/foo.page-abc1234";

        [Fact]
        public async Task Opens_a_pull_request_against_the_production_branch()
        {
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", body =>
                {
                    var json = JObject.Parse(body);
                    Assert.Equal(Branch, json["head"]?.Value<string>());
                    Assert.Equal("master", json["base"]?.Value<string>());
                    Assert.Equal("publish foo", json["title"]?.Value<string>());
                    return RespondJson(PullRequest(), HttpStatusCode.Created);
                }),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: true))),
                ("POST", "graphql", _ => RespondJson("""{ "data": {} }""")));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Pending, result.State);
            Assert.Equal(7, result.PullRequestNumber);
            Assert.Equal("https://github.test/pr/7", result.Url);
            handler.AssertDone();
        }

        [Fact]
        public async Task Nothing_to_merge_is_already_published_not_an_error()
        {
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => NothingToMerge()),
                ("GET", $"repos/o/r/pulls?state=open&head={Uri.EscapeDataString($"o:{Branch}")}", _ => RespondJson("[]")));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.AlreadyPublished, result.State);
            Assert.Null(result.PullRequestNumber);
            handler.AssertDone();
        }

        [Fact]
        public async Task An_open_pull_request_is_reused_rather_than_duplicated()
        {
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => PullRequestAlreadyOpen()),
                ("GET", $"repos/o/r/pulls?state=open&head={Uri.EscapeDataString($"o:{Branch}")}", _ => RespondJson($"[{PullRequest()}]")),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: true))),
                ("POST", "graphql", _ => RespondJson("""{ "data": {} }""")));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Pending, result.State);
            handler.AssertDone();
        }

        [Fact]
        public async Task A_page_that_moved_in_the_production_branch_is_a_conflict()
        {
            // silently taking either side would throw away someone's work
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => RespondJson(PullRequest(), HttpStatusCode.Created)),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: false))));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Conflict, result.State);
            Assert.Equal(7, result.PullRequestNumber);
            handler.AssertDone();
        }

        [Fact]
        public async Task Waits_while_github_computes_mergeability()
        {
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => RespondJson(PullRequest(), HttpStatusCode.Created)),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest())),               // mergeable: null
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: false))));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Conflict, result.State);
            handler.AssertDone();
        }

        [Fact]
        public async Task Merges_outright_when_auto_merge_is_refused()
        {
            // the repository has auto-merge off, or nothing is left to wait for
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => RespondJson(PullRequest(), HttpStatusCode.Created)),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: true))),
                ("POST", "graphql", _ => RespondJson("""{ "errors": [ { "message": "Pull request is in clean status" } ] }""")),
                ("PUT", "repos/o/r/pulls/7/merge", body =>
                {
                    Assert.Equal("squash", JObject.Parse(body)["merge_method"]?.Value<string>());
                    return RespondJson("""{ "merged": true }""");
                }));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Merged, result.State);
            handler.AssertDone();
        }

        [Fact]
        public async Task A_blocked_merge_is_pending_never_published()
        {
            // 405: a required check has not passed. Reporting "published" here would be a lie.
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => RespondJson(PullRequest(), HttpStatusCode.Created)),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: true))),
                ("POST", "graphql", _ => RespondJson("""{ "errors": [ { "message": "nope" } ] }""")),
                ("PUT", "repos/o/r/pulls/7/merge", _ => Respond(HttpStatusCode.MethodNotAllowed)),
                // the refusal is re-read: 405 is also what a conflict answers, and the two mean opposite things
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: true))));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Pending, result.State);
            handler.AssertDone();
        }

        [Fact]
        public async Task A_merge_refused_because_the_page_conflicts_is_a_conflict_not_pending()
        {
            // GitHub answers 405 both to "a check has not passed" and to "this pull request conflicts",
            // and it had to finish computing mergeability to refuse at all — so the answer it withheld
            // while we waited for it is available now. Reporting Pending would leave the editor watching
            // "Publishing…" over a page that will never merge on its own.
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => RespondJson(PullRequest(), HttpStatusCode.Created)),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest())),                  // mergeable: null, still computing
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest())),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest())),
                ("POST", "graphql", _ => RespondJson("""{ "errors": [ { "message": "nope" } ] }""")),
                ("PUT", "repos/o/r/pulls/7/merge", _ => Respond(HttpStatusCode.MethodNotAllowed)),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: false))));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Conflict, result.State);
            Assert.Equal(7, result.PullRequestNumber);
            handler.AssertDone();
        }

        [Fact]
        public async Task An_already_merged_pull_request_reports_merged()
        {
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => PullRequestAlreadyOpen()),
                ("GET", $"repos/o/r/pulls?state=open&head={Uri.EscapeDataString($"o:{Branch}")}", _ => RespondJson($"[{PullRequest()}]")),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson("""{ "number": 7, "merged": true }""")));

            var result = await Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Merged, result.State);
            handler.AssertDone();
        }

        [Fact]
        public async Task GetOpenPullRequestNumberAsync_finds_the_page_on_its_way_to_production()
        {
            var handler = new ScriptedHandler(
                ("GET", $"repos/o/r/pulls?state=open&head={Uri.EscapeDataString($"o:{Branch}")}", _ => RespondJson($"[{PullRequest()}]")));

            Assert.Equal(7, await Create(handler).GetOpenPullRequestNumberAsync(Branch, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetOpenPullRequestNumberAsync_is_null_when_nothing_is_in_flight()
        {
            var handler = new ScriptedHandler(
                ("GET", $"repos/o/r/pulls?state=open&head={Uri.EscapeDataString($"o:{Branch}")}", _ => RespondJson("[]")));

            Assert.Null(await Create(handler).GetOpenPullRequestNumberAsync(Branch, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task An_api_failure_that_is_not_422_throws()
        {
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => Respond(HttpStatusCode.InternalServerError)));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task A_422_that_is_not_about_this_branch_throws_rather_than_reporting_published()
        {
            // a repository rule, a base branch that does not exist, a token without the rights — GitHub
            // says 422 to all of them. Reading that as "nothing to merge" would tell the editor their
            // page is live when no pull request was ever opened.
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", _ => Respond422("Base branch was modified. Review and try the merge again.")));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                Create(handler).MergeBranchAsync(Branch, "publish foo", TestContext.Current.CancellationToken));
            handler.AssertDone();
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Promotion ships into the release branch, and the only thing that distinguishes it from a publish
        /// is which branch the pull request targets. If this ever fell back to BaseBranch the page would be
        /// merged straight back into the branch it came from — a no-op reported as a successful promotion,
        /// with production never hearing about it.
        /// </summary>
        [Fact]
        public async Task Merging_into_a_named_branch_targets_that_branch_not_the_base()
        {
            const string promoteBranch = "promote/foo.page-abc1234";
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/pulls", body =>
                {
                    var json = JObject.Parse(body);
                    Assert.Equal(promoteBranch, json["head"]?.Value<string>());
                    Assert.Equal("release", json["base"]?.Value<string>());
                    return RespondJson(PullRequest(), HttpStatusCode.Created);
                }),
                ("GET", "repos/o/r/pulls/7", _ => RespondJson(PullRequest(mergeable: true))),
                ("POST", "graphql", _ => RespondJson("""{ "data": {} }""")));

            var result = await Create(handler)
                .MergeBranchIntoAsync(promoteBranch, "promote foo", "release", TestContext.Current.CancellationToken);

            Assert.Equal(GitPublishState.Pending, result.State);
            handler.AssertDone();
        }

        /// <summary>
        /// A blank base is refused rather than quietly treated as the publish branch: the caller that
        /// forgot to configure a release branch would otherwise promote into dev and be told it worked.
        /// </summary>
        [Fact]
        public async Task Merging_into_a_blank_branch_is_refused()
        {
            var publisher = Create(new ScriptedHandler());

            await Assert.ThrowsAsync<ArgumentException>(() =>
                publisher.MergeBranchIntoAsync("promote/foo", "promote foo", "  ",
                    TestContext.Current.CancellationToken));
        }

        private static GitHubContentPublisher Create(ScriptedHandler handler)
        {
            var options = Options.Create(new GitContentOptions
            {
                Repository = "o/r",
                BaseBranch = "master",
                GraphQlUrl = new Uri("https://api.github.test/graphql"),
            });
            return new GitHubContentPublisher(new FakeHttpClientFactory(handler), options);
        }

        private static string PullRequest(bool? mergeable = null)
        {
            var json = new JObject
            {
                ["number"] = 7,
                ["node_id"] = "PR_node",
                ["html_url"] = "https://github.test/pr/7",
                ["merged"] = false,
                ["mergeable"] = mergeable.HasValue ? mergeable.Value : JValue.CreateNull(),
            };
            return json.ToString();
        }

        private static HttpResponseMessage Respond(HttpStatusCode status) =>
            new(status) { Content = new StringContent("{}") };

        // GitHub says 422 to several unrelated things when a pull request is opened, and the body is the
        // only way to tell them apart — so the tests have to carry a real one.
        private static HttpResponseMessage Respond422(string message)
        {
            var body = new JObject
            {
                ["message"] = "Validation Failed",
                ["errors"] = new JArray { new JObject { ["message"] = message } },
            };
            return RespondJson(body.ToString(), HttpStatusCode.UnprocessableEntity);
        }

        private static HttpResponseMessage NothingToMerge() =>
            Respond422($"No commits between master and {Branch}");

        private static HttpResponseMessage PullRequestAlreadyOpen() =>
            Respond422($"A pull request already exists for o:{Branch}.");

        private static HttpResponseMessage RespondJson(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) =>
                new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.github.test/") };
        }

        private sealed class ScriptedHandler(params (string Method, string Url, Func<string, HttpResponseMessage> Respond)[] script) : HttpMessageHandler
        {
            private int _step;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.True(_step < script.Length, $"Unexpected request #{_step + 1}: {request.Method} {request.RequestUri}");
                var (method, url, respond) = script[_step++];
                Assert.Equal(method, request.Method.Method);
                Assert.Equal(url, request.RequestUri!.PathAndQuery.TrimStart('/'));
                var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return respond(body);
            }

            public void AssertDone() => Assert.Equal(script.Length, _step);
        }
    }
}
