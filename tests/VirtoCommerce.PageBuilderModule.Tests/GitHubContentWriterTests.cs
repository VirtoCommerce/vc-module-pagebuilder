using System;
using System.Linq;
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
    public class GitHubContentWriterTests
    {
        private static readonly GitCommitAuthor Author = new() { Name = "Editor One", Email = "editor@example.com" };

        [Fact]
        public async Task CommitFileAsync_NewBranchAndNewFile_CreatesBranchAndPutsWithoutSha()
        {
            var handler = new ScriptedHandler(
                // branch check → missing
                ("GET", "repos/o/r/git/ref/heads/designer/editor", _ => Respond(HttpStatusCode.NotFound)),
                // base branch → sha
                ("GET", "repos/o/r/git/ref/heads/master", _ => RespondJson("""{ "object": { "sha": "base-sha" } }""")),
                // create branch
                ("POST", "repos/o/r/git/refs", req =>
                {
                    var body = JObject.Parse(req);
                    Assert.Equal("refs/heads/designer/editor", body["ref"]?.Value<string>());
                    Assert.Equal("base-sha", body["sha"]?.Value<string>());
                    return RespondJson("{}", HttpStatusCode.Created);
                }),
                // file sha on the branch → new file
                ("GET", "repos/o/r/contents/pages/docs/foo.page?ref=designer%2Feditor", _ => Respond(HttpStatusCode.NotFound)),
                // upsert
                ("PUT", "repos/o/r/contents/pages/docs/foo.page", req =>
                {
                    var body = JObject.Parse(req);
                    Assert.Null(body["sha"]);
                    Assert.Equal("designer/editor", body["branch"]?.Value<string>());
                    Assert.Equal("Editor One", body["author"]?["name"]?.Value<string>());
                    Assert.Equal("{ \"a\": 1 }", Encoding.UTF8.GetString(Convert.FromBase64String(body["content"]!.Value<string>()!)));
                    return RespondJson("{}", HttpStatusCode.Created);
                }));

            await CreateWriter(handler).CommitFileAsync("pages/docs/foo.page", "{ \"a\": 1 }", "designer/editor", "msg", Author, TestContext.Current.CancellationToken);

            handler.AssertDone();
        }

        [Fact]
        public async Task CommitFileAsync_ExistingFile_PutsWithSha()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/git/ref/heads/designer/editor", _ => RespondJson("""{ "object": { "sha": "tip" } }""")),
                ("GET", "repos/o/r/contents/pages/foo.page?ref=designer%2Feditor", _ => RespondJson("""{ "sha": "old-file-sha" }""")),
                ("PUT", "repos/o/r/contents/pages/foo.page", req =>
                {
                    Assert.Equal("old-file-sha", JObject.Parse(req)["sha"]?.Value<string>());
                    return RespondJson("{}");
                }));

            await CreateWriter(handler).CommitFileAsync("pages/foo.page", "{}", "designer/editor", "msg", Author, TestContext.Current.CancellationToken);

            handler.AssertDone();
        }

        [Fact]
        public async Task CommitFileAsync_StaleSha_RefreshesAndRetriesOnce()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/git/ref/heads/designer/editor", _ => RespondJson("""{ "object": { "sha": "tip" } }""")),
                ("GET", "repos/o/r/contents/pages/foo.page?ref=designer%2Feditor", _ => RespondJson("""{ "sha": "stale" }""")),
                ("PUT", "repos/o/r/contents/pages/foo.page", _ => Respond(HttpStatusCode.Conflict)),
                ("GET", "repos/o/r/contents/pages/foo.page?ref=designer%2Feditor", _ => RespondJson("""{ "sha": "fresh" }""")),
                ("PUT", "repos/o/r/contents/pages/foo.page", req =>
                {
                    Assert.Equal("fresh", JObject.Parse(req)["sha"]?.Value<string>());
                    return RespondJson("{}");
                }));

            await CreateWriter(handler).CommitFileAsync("pages/foo.page", "{}", "designer/editor", "msg", Author, TestContext.Current.CancellationToken);

            handler.AssertDone();
        }

        [Fact]
        public async Task CommitFileAsync_ApiFailure_Throws()
        {
            var handler = new ScriptedHandler(
                ("GET", "repos/o/r/git/ref/heads/designer/editor", _ => Respond(HttpStatusCode.Unauthorized)));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                CreateWriter(handler).CommitFileAsync("pages/foo.page", "{}", "designer/editor", "msg", Author, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task CommitFileAsync_RepositoryNotConfigured_Throws()
        {
            var writer = CreateWriter(new ScriptedHandler(), repository: null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                writer.CommitFileAsync("pages/foo.page", "{}", "designer/editor", "msg", Author, TestContext.Current.CancellationToken));
        }

        private static GitHubContentWriter CreateWriter(ScriptedHandler handler, string repository = "o/r")
        {
            var options = Options.Create(new GitContentOptions { Repository = repository, BaseBranch = "master" });
            return new GitHubContentWriter(new FakeHttpClientFactory(handler), options);
        }

        private static HttpResponseMessage Respond(HttpStatusCode status)
        {
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }

        private static HttpResponseMessage RespondJson(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.github.test/") };
            }
        }

        /// <summary>
        /// Asserts the exact sequence of (method, relative url) requests and lets each step
        /// inspect the request body and produce the response.
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

            public void AssertDone()
            {
                Assert.Equal(script.Length, _step);
            }
        }
    }
}
