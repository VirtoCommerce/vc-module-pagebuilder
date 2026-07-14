using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class GitHubContentRepositoryTests
    {
        private static readonly GitCommitAuthor Author = new() { Name = "Editor One", Email = "editor@example.com" };

        private const string Sha = "0123456789abcdef0123456789abcdef01234567";

        // ── reading ────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task ReadFileAsync_DecodesBase64Content()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=master", _ => RespondJson(ContentsPayload("{ \"a\": 1 }"))));

            var content = await Create(handler).ReadFileAsync("pages/foo.page", "master", TestContext.Current.CancellationToken);

            Assert.Equal("{ \"a\": 1 }", content);
            handler.AssertDone();
        }

        [Fact]
        public async Task ReadFileAsync_MissingFileOrRef_IsNullRatherThanAnError()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=designer%2Fjohn%2Ffoo", _ => Respond(HttpStatusCode.NotFound)));

            Assert.Null(await Create(handler).ReadFileAsync("pages/foo.page", "designer/john/foo", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ReadFileAsync_CachesTheAnswer_IncludingAMiss()
        {
            // one scripted request: a second call that reached GitHub would trip the handler's assertion
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=master", _ => Respond(HttpStatusCode.NotFound)));
            var repository = Create(handler);

            Assert.Null(await repository.ReadFileAsync("pages/foo.page", "master", TestContext.Current.CancellationToken));
            Assert.Null(await repository.ReadFileAsync("pages/foo.page", "master", TestContext.Current.CancellationToken));

            handler.AssertDone();
        }

        [Fact]
        public async Task CommitFileAsync_DropsTheCachedReadOfThatBranch()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => RespondJson(ContentsPayload("old"))),
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => RespondJson("""{ "sha": "file-sha" }""")),
                ("PUT", "repos/o/r/contents/pages/foo.page", _ => RespondJson(CommitPayload())),
                // the stale entry is gone, so this read goes to GitHub again
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => RespondJson(ContentsPayload("new"))));
            var repository = Create(handler);

            Assert.Equal("old", await repository.ReadFileAsync("pages/foo.page", "work", TestContext.Current.CancellationToken));
            await repository.CommitFileAsync("pages/foo.page", "new", "work", "msg", Author, TestContext.Current.CancellationToken);

            Assert.Equal("new", await repository.ReadFileAsync("pages/foo.page", "work", TestContext.Current.CancellationToken));
            handler.AssertDone();
        }

        // ── branches ───────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetBranchHeadShaAsync_MissingBranch_IsNull()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/git/ref/heads/designer/john/foo", _ => Respond(HttpStatusCode.NotFound)));

            Assert.Null(await Create(handler).GetBranchHeadShaAsync("designer/john/foo", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task CreateBranchAsync_ResolvesTheSourceBranchThenCutsTheRef()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/git/ref/heads/master", _ => RespondJson($$"""{ "object": { "sha": "{{Sha}}" } }""")),
                ("POST", "repos/o/r/git/refs", body =>
                {
                    var json = JObject.Parse(body);
                    Assert.Equal("refs/heads/designer/john/foo", json["ref"]?.Value<string>());
                    Assert.Equal(Sha, json["sha"]?.Value<string>());
                    return RespondJson("{}", HttpStatusCode.Created);
                }));

            await Create(handler).CreateBranchAsync("designer/john/foo", "master", TestContext.Current.CancellationToken);
            handler.AssertDone();
        }

        [Fact]
        public async Task CreateBranchAsync_FromASha_SkipsTheLookup()
        {
            var handler = new ScriptedHandler(
                ("POST", "repos/o/r/git/refs", body =>
                {
                    Assert.Equal(Sha, JObject.Parse(body)["sha"]?.Value<string>());
                    return RespondJson("{}", HttpStatusCode.Created);
                }));

            await Create(handler).CreateBranchAsync("designer/john/foo", Sha, TestContext.Current.CancellationToken);
            handler.AssertDone();
        }

        [Fact]
        public async Task CreateBranchAsync_AlreadyExists_IsNotAnError()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/git/ref/heads/master", _ => RespondJson($$"""{ "object": { "sha": "{{Sha}}" } }""")),
                ("POST", "repos/o/r/git/refs", _ => Respond422("Reference already exists")));

            await Create(handler).CreateBranchAsync("designer/john/foo", "master", TestContext.Current.CancellationToken);
            handler.AssertDone();
        }

        [Fact]
        public async Task CreateBranchAsync_A422ThatIsNotAboutAnExistingRef_Throws()
        {
            // GitHub says 422 to a name git refuses and to a sha it cannot find as well. Swallowing those
            // would report a branch that was never cut, and the commit that follows would fail instead —
            // somewhere else, with a worse message.
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/git/ref/heads/master", _ => RespondJson($$"""{ "object": { "sha": "{{Sha}}" } }""")),
                ("POST", "repos/o/r/git/refs", _ => Respond422("Object does not exist")));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                Create(handler).CreateBranchAsync("designer/john/foo", "master", TestContext.Current.CancellationToken));
            handler.AssertDone();
        }

        [Fact]
        public async Task CreateBranchAsync_UnresolvableSource_Throws()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/git/ref/heads/nope", _ => Respond(HttpStatusCode.NotFound)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Create(handler).CreateBranchAsync("designer/john/foo", "nope", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DeleteBranchAsync_AlreadyGone_IsNotAnError()
        {
            var handler = new ScriptedHandler(
                ("DELETE", "repos/o/r/git/refs/heads/designer/john/foo", _ => Respond(HttpStatusCode.NotFound)));

            await Create(handler).DeleteBranchAsync("designer/john/foo", "pages/foo.page", TestContext.Current.CancellationToken);
            handler.AssertDone();
        }

        [Fact]
        public async Task DeleteBranchAsync_DropsTheCachedDraftOfThatBranch()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=designer%2Fjohn%2Ffoo", _ => RespondJson(ContentsPayload("draft"))),
                ("DELETE", "repos/o/r/git/refs/heads/designer/john/foo", _ => Respond(HttpStatusCode.NoContent)),
                // the draft is gone from GitHub; the read must go back and see that, not serve the cache
                ("GET", "repos/o/r/contents/pages/foo.page?ref=designer%2Fjohn%2Ffoo", _ => Respond(HttpStatusCode.NotFound)));
            var repository = Create(handler);

            Assert.Equal("draft", await repository.ReadFileAsync("pages/foo.page", "designer/john/foo", TestContext.Current.CancellationToken));
            await repository.DeleteBranchAsync("designer/john/foo", "pages/foo.page", TestContext.Current.CancellationToken);

            Assert.Null(await repository.ReadFileAsync("pages/foo.page", "designer/john/foo", TestContext.Current.CancellationToken));
            handler.AssertDone();
        }

        [Fact]
        public async Task InvalidateRead_SendsTheNextReadOfThatRefBackToGit()
        {
            // the publisher merges behind the repository's back, so the production branch moves without
            // any write going through here — the cached read of it has to be dropped explicitly
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=master", _ => RespondJson(ContentsPayload("before"))),
                ("GET", "repos/o/r/contents/pages/foo.page?ref=master", _ => RespondJson(ContentsPayload("after"))));
            var repository = Create(handler);

            Assert.Equal("before", await repository.ReadFileAsync("pages/foo.page", "master", TestContext.Current.CancellationToken));
            repository.InvalidateRead("pages/foo.page", "master");

            Assert.Equal("after", await repository.ReadFileAsync("pages/foo.page", "master", TestContext.Current.CancellationToken));
            handler.AssertDone();
        }

        // ── writing ────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CommitFileAsync_NewFile_PutsWithoutShaAndReturnsTheCommit()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/docs/foo.page?ref=work", _ => Respond(HttpStatusCode.NotFound)),
                ("PUT", "repos/o/r/contents/pages/docs/foo.page", body =>
                {
                    var json = JObject.Parse(body);
                    Assert.Null(json["sha"]);
                    Assert.Equal("work", json["branch"]?.Value<string>());
                    Assert.Equal("{ \"a\": 1 }", Encoding.UTF8.GetString(Convert.FromBase64String(json["content"]!.Value<string>()!)));
                    Assert.Equal("Editor One", json["author"]?["name"]?.Value<string>());
                    return RespondJson(CommitPayload());
                }));

            var commit = await Create(handler).CommitFileAsync("pages/docs/foo.page", "{ \"a\": 1 }", "work", "msg", Author, TestContext.Current.CancellationToken);

            Assert.Equal(Sha, commit);
            handler.AssertDone();
        }

        [Fact]
        public async Task CommitFileAsync_ExistingFile_PutsWithSha()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => RespondJson("""{ "sha": "file-sha" }""")),
                ("PUT", "repos/o/r/contents/pages/foo.page", body =>
                {
                    Assert.Equal("file-sha", JObject.Parse(body)["sha"]?.Value<string>());
                    return RespondJson(CommitPayload());
                }));

            await Create(handler).CommitFileAsync("pages/foo.page", "{}", "work", "msg", Author, TestContext.Current.CancellationToken);
            handler.AssertDone();
        }

        [Fact]
        public async Task CommitFileAsync_StaleSha_RefreshesAndRetriesOnce()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => RespondJson("""{ "sha": "stale" }""")),
                ("PUT", "repos/o/r/contents/pages/foo.page", _ => Respond(HttpStatusCode.Conflict)),
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => RespondJson("""{ "sha": "fresh" }""")),
                ("PUT", "repos/o/r/contents/pages/foo.page", body =>
                {
                    Assert.Equal("fresh", JObject.Parse(body)["sha"]?.Value<string>());
                    return RespondJson(CommitPayload());
                }));

            await Create(handler).CommitFileAsync("pages/foo.page", "{}", "work", "msg", Author, TestContext.Current.CancellationToken);
            handler.AssertDone();
        }

        [Fact]
        public async Task CommitFileAsync_DoesNotCreateTheBranch()
        {
            // publishing a page must ship that page and nothing else, so the work branch is cut
            // deliberately by the caller from the production branch — never implied by a save
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => Respond(HttpStatusCode.NotFound)),
                ("PUT", "repos/o/r/contents/pages/foo.page", _ => RespondJson(CommitPayload())));

            await Create(handler).CommitFileAsync("pages/foo.page", "{}", "work", "msg", Author, TestContext.Current.CancellationToken);

            handler.AssertDone(); // the script contains no git/ref calls
        }

        [Fact]
        public async Task CommitFileAsync_ApiFailure_Throws()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => Respond(HttpStatusCode.NotFound)),
                ("PUT", "repos/o/r/contents/pages/foo.page", _ => Respond(HttpStatusCode.InternalServerError)));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                Create(handler).CommitFileAsync("pages/foo.page", "{}", "work", "msg", Author, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DeleteFileAsync_SendsTheFileSha()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => RespondJson("""{ "sha": "file-sha" }""")),
                ("DELETE", "repos/o/r/contents/pages/foo.page", body =>
                {
                    var json = JObject.Parse(body);
                    Assert.Equal("file-sha", json["sha"]?.Value<string>());
                    Assert.Equal("work", json["branch"]?.Value<string>());
                    return RespondJson(CommitPayload());
                }));

            var commit = await Create(handler).DeleteFileAsync("pages/foo.page", "work", "msg", Author, TestContext.Current.CancellationToken);

            Assert.Equal(Sha, commit);
            handler.AssertDone();
        }

        [Fact]
        public async Task DeleteFileAsync_MissingFile_Throws()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/contents/pages/foo.page?ref=work", _ => Respond(HttpStatusCode.NotFound)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Create(handler).DeleteFileAsync("pages/foo.page", "work", "msg", Author, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task RepositoryNotConfigured_Throws()
        {
            var repository = Create(new ScriptedHandler(), repository: null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.CommitFileAsync("pages/foo.page", "{}", "work", "msg", Author, TestContext.Current.CancellationToken));
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static GitHubContentRepository Create(ScriptedHandler handler, string repository = "o/r")
        {
            var options = Options.Create(new GitContentOptions { Repository = repository, BaseBranch = "master" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            return new GitHubContentRepository(new FakeHttpClientFactory(handler), cache, options);
        }

        private static string ContentsPayload(string content) =>
            new JObject { ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) }.ToString();

        private static string CommitPayload() => $$"""{ "commit": { "sha": "{{Sha}}" } }""";

        private static HttpResponseMessage Respond(HttpStatusCode status) =>
            new(status) { Content = new StringContent("{}") };

        // The body is the only thing that separates "the branch is already there" from a 422 that means
        // the branch was not created at all, so the tests have to carry a real one.
        private static HttpResponseMessage Respond422(string message)
        {
            var body = new JObject
            {
                ["message"] = "Validation Failed",
                ["errors"] = new JArray { new JObject { ["message"] = message } },
            };
            return RespondJson(body.ToString(), HttpStatusCode.UnprocessableEntity);
        }

        private static HttpResponseMessage RespondJson(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) =>
                new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.github.test/") };
        }

        /// <summary>
        /// Asserts the exact sequence of (method, relative url) requests and lets each step inspect the
        /// request body and produce the response.
        /// </summary>
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
