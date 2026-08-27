using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// The payloads mirror what the content repository actually answers (measured 2026-08-17 against
    /// VirtoCommerce/vc-content): branches share history, so one commit arrives on many of them, and a
    /// page's oldest unpublished "version" can be a 366-file import commit.
    /// </summary>
    public class GitHubContentHistoryTests
    {
        private const string Path = "pages/about-us.page";

        // shas as they came back from the repository, so the fixtures read like the real thing
        private const string PublishedSeed = "82885e7c9f1e4a2b8d3c5f6a7b8c9d0e1f2a3b4c";
        private const string PublishedCanonical = "192aec3d4e5f60718293a4b5c6d7e8f90a1b2c3d";
        private const string BulkImport = "dfa474d1e2f3a4b5c6d7e8f90a1b2c3d4e5f6071";
        private const string DesignerSave = "81985bea86fe7618cc1a959c76fd031d9e3e9b5a";
        private const string ExternalEdit = "70a3c34b5c6d7e8f90a1b2c3d4e5f60718293a4b";

        [Fact]
        public async Task GetHistoryAsync_OneRequest_ListsPublishedAndUnpublishedVersions()
        {
            var handler = Handler(Payload(
                published: [Commit(PublishedCanonical, "2026-07-09T20:37:43Z", "chore: store pages in a canonical form", files: 252)],
                branches:
                [
                    ("designer/john/about-us-1a2b3c4", [Commit(DesignerSave, "2026-08-11T14:40:10Z", "designer: save /about-us.page")]),
                ]));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            Assert.Equal(1, handler.RequestCount);
            Assert.Collection(history.Versions,
                // unpublished first: someone's work the production branch does not have
                version =>
                {
                    Assert.Equal(DesignerSave, version.Sha);
                    Assert.Equal("81985be", version.ShortSha);
                    Assert.False(version.Published);
                    Assert.Equal(["designer/john/about-us-1a2b3c4"], version.Branches);
                },
                version =>
                {
                    Assert.Equal(PublishedCanonical, version.Sha);
                    Assert.True(version.Published);
                });
            Assert.False(history.Truncated);
        }

        [Fact]
        public async Task GetHistoryAsync_TheSameCommitOnManyBranches_IsOneVersionThatNamesThemAll()
        {
            // measured: a single page edit was reachable from fourteen branches at once, and one 2021
            // commit from forty-six. Without folding, the list is mostly the same version repeated.
            var branchNames = Enumerable.Range(1, 14).Select(i => $"content/audit-{i:00}").ToArray();
            var handler = Handler(Payload(
                published: [],
                branches: branchNames.Select(name =>
                    (name, new[] { Commit(BulkImport, "2026-07-29T11:29:46Z", "pages from dev environment: content", files: 366) })).ToArray()));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            var version = Assert.Single(history.Versions);
            Assert.Equal(BulkImport, version.Sha);
            Assert.Equal(14, version.Branches.Count);
            Assert.Equal(branchNames.OrderBy(name => name, StringComparer.Ordinal), version.Branches);
        }

        [Fact]
        public async Task GetHistoryAsync_ACommitTheProductionBranchAlsoHas_IsPublishedAndNamesItFirst()
        {
            // the same sha on both sides: branches are cut from the production branch, so most of what a
            // branch reports is already live and must not be offered as a draft
            var handler = Handler(Payload(
                published: [Commit(PublishedSeed, "2026-07-09T15:39:24Z", "feat: seed pages from the dev environment", files: 252)],
                branches: [("designer/john/about-us-1a2b3c4", [Commit(PublishedSeed, "2026-07-09T15:39:24Z", "feat: seed pages from the dev environment", files: 252)])]));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            var version = Assert.Single(history.Versions);
            Assert.True(version.Published);
            Assert.Equal(["master", "designer/john/about-us-1a2b3c4"], version.Branches);
        }

        [Fact]
        public async Task GetHistoryAsync_TheProductionBranchAmongTheRefs_IsNotCountedTwice()
        {
            // refs/heads/ enumerates every branch, the production branch included; reading it again would
            // list the production branch as a draft branch of its own commits
            var handler = Handler(Payload(
                published: [Commit(PublishedCanonical, "2026-07-09T20:37:43Z", "chore: canonical form", files: 252)],
                branches: [("master", [Commit(PublishedCanonical, "2026-07-09T20:37:43Z", "chore: canonical form", files: 252)])]));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            var version = Assert.Single(history.Versions);
            Assert.True(version.Published);
            Assert.Equal(["master"], version.Branches);
        }

        [Fact]
        public async Task GetHistoryAsync_ACommitThatTouchedFarMoreThanThisPage_IsFlaggedBulk()
        {
            var handler = Handler(Payload(
                published: [],
                branches:
                [
                    ("content/seed", [Commit(BulkImport, "2026-07-29T11:29:46Z", "pages from dev environment: content", files: 366)]),
                    ("designer/john/about-us-1a2b3c4", [Commit(DesignerSave, "2026-08-11T14:40:10Z", "designer: save /about-us.page", files: 1)]),
                ]));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            Assert.True(history.Versions.Single(version => version.Sha == BulkImport).Bulk);
            Assert.False(history.Versions.Single(version => version.Sha == DesignerSave).Bulk);
        }

        [Fact]
        public async Task GetHistoryAsync_UnknownChangedFileCount_IsNotBulk()
        {
            var commit = Commit(DesignerSave, "2026-08-11T14:40:10Z", "designer: save /about-us.page");
            commit["changedFilesIfAvailable"] = null;
            var handler = Handler(Payload(published: [], branches: [("designer/john/about-us-1a2b3c4", [commit])]));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            var version = Assert.Single(history.Versions);
            Assert.Null(version.ChangedFiles);
            Assert.False(version.Bulk);
        }

        [Fact]
        public async Task GetHistoryAsync_ReadsAuthorshipBothWays()
        {
            // an edit made outside the builder carries the person's own git identity and resolves to a
            // GitHub account; a commit the module made is signed with the shared fallback address and
            // carries the platform login in a trailer instead
            var external = Commit(ExternalEdit, "2026-08-11T14:02:42Z", "Epicor: publish current constructor draft");
            external["author"] = Author("Svetlana Tumanova", "svetlana.tumanova@virtoworks.com", "svetlana-virto");

            var module = Commit(DesignerSave, "2026-08-11T14:40:10Z", "designer: save /about-us.page");
            module["author"] = Author("john", "pagebuilder-noreply@virtocommerce.com", login: null);
            module["messageBody"] = "…: vccom, by: john)\n\nVC-User: john";

            var handler = Handler(Payload(published: [], branches: [("content/hero-copy", [external]), ("designer/john/about-us-1a2b3c4", [module])]));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            var fromClaude = history.Versions.Single(version => version.Sha == ExternalEdit);
            Assert.Equal("svetlana.tumanova@virtoworks.com", fromClaude.AuthorEmail);
            Assert.Equal("svetlana-virto", fromClaude.AuthorLogin);
            Assert.Null(fromClaude.VcUser);

            var fromBuilder = history.Versions.Single(version => version.Sha == DesignerSave);
            Assert.Null(fromBuilder.AuthorLogin);
            Assert.Equal("john", fromBuilder.VcUser);
            Assert.Equal(new DateTime(2026, 8, 11, 14, 40, 10, DateTimeKind.Utc), fromBuilder.Date);
        }

        [Fact]
        public async Task GetHistoryAsync_MoreBranchesThanOnePage_SaysSoAndHandsBackTheCursor()
        {
            var handler = Handler(Payload(
                published: [],
                branches: [("designer/john/about-us-1a2b3c4", [Commit(DesignerSave, "2026-08-11T14:40:10Z", "designer: save")])],
                hasNextPage: true,
                endCursor: "Y3Vyc29yOnYyOpK5"));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            Assert.True(history.Truncated);
            Assert.Equal("Y3Vyc29yOnYyOpK5", history.EndCursor);
        }

        [Fact]
        public async Task GetHistoryAsync_APageNoBranchTouched_IsAnEmptyListRatherThanAFailure()
        {
            var handler = Handler(Payload(published: [], branches: [("dependabot/npm_and_yarn/vite-6", [])]));

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            Assert.Empty(history.Versions);
        }

        [Fact]
        public async Task GetHistoryAsync_APageMissingFromTheProductionBranch_HasOnlyDrafts()
        {
            // a page created in the designer and never published: GitHub answers null for the ref target
            var payload = Payload(published: [], branches: [("designer/john/about-us-1a2b3c4", [Commit(DesignerSave, "2026-08-11T14:40:10Z", "designer: save")])]);
            payload["data"]["repository"]["published"] = null;
            var handler = Handler(payload);

            var history = await Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            Assert.DoesNotContain(history.Versions, version => version.Published);
            Assert.Single(history.Versions);
        }

        [Fact]
        public async Task GetHistoryAsync_SendsTheConfiguredSizesAndTheCursor()
        {
            var handler = Handler(Payload(published: [], branches: []));
            var options = Options(configure: o =>
            {
                o.MaxBranches = 50;
                o.CommitsPerBranch = 3;
                o.PublishedHistoryDepth = 25;
            });

            await Create(handler, options).GetHistoryAsync(Path, new GitHistoryQuery { BaseBranch = "master", After = "cursor" }, TestContext.Current.CancellationToken);

            var variables = JObject.Parse(handler.LastBody)["variables"];
            Assert.Equal("o", variables["owner"]);
            Assert.Equal("r", variables["name"]);
            Assert.Equal(Path, variables["path"]);
            Assert.Equal("master", variables["base"]);
            Assert.Equal(25, variables["take"]);
            Assert.Equal(50, variables["refs"]);
            Assert.Equal(3, variables["perBranch"]);
            Assert.Equal("cursor", variables["after"]);
        }

        [Fact]
        public async Task GetHistoryAsync_ExplicitDepth_OverridesTheConfiguredOne()
        {
            var handler = Handler(Payload(published: [], branches: []));

            await Create(handler).GetHistoryAsync(Path, new GitHistoryQuery { BaseBranch = "master", PublishedDepth = 5 }, TestContext.Current.CancellationToken);

            Assert.Equal(5, JObject.Parse(handler.LastBody)["variables"]["take"]);
        }

        [Fact]
        public async Task GetHistoryAsync_CachesTheAnswer()
        {
            var handler = Handler(Payload(published: [], branches: []));
            var history = Create(handler);

            await history.GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);
            await history.GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);

            Assert.Equal(1, handler.RequestCount);
        }

        [Fact]
        public async Task Invalidate_MakesTheNextCallReadTheRepositoryAgain()
        {
            // what a save has to do: the commit the editor just made is in the repository, and a version
            // list still serving the cached answer would be denying their work exists
            var handler = Handler(
                Payload(published: [], branches: []),
                Payload(published: [], branches: [("designer/john/about-us-1a2b3c4", [Commit(DesignerSave, "2026-08-11T14:40:10Z", "designer: save")])]));
            var history = Create(handler);

            Assert.Empty((await history.GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken)).Versions);
            history.Invalidate(Path);

            Assert.Single((await history.GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken)).Versions);
            Assert.Equal(2, handler.RequestCount);
        }

        [Fact]
        public async Task Invalidate_LeavesOtherPagesCached()
        {
            var handler = Handler(Payload(published: [], branches: []), Payload(published: [], branches: []));
            var history = Create(handler);

            await history.GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken);
            await history.GetHistoryAsync("pages/contact-us.page", Query(), TestContext.Current.CancellationToken);
            history.Invalidate(Path);
            await history.GetHistoryAsync("pages/contact-us.page", Query(), TestContext.Current.CancellationToken);

            Assert.Equal(2, handler.RequestCount);
        }

        [Fact]
        public async Task GetHistoryAsync_GraphQlErrorInsideASuccessfulResponse_Throws()
        {
            // "no other versions" and "the query failed" must not look the same to a caller
            var handler = Handler(new JObject
            {
                ["data"] = null,
                ["errors"] = new JArray { new JObject { ["message"] = "Could not resolve to a Repository with the name 'o/r'." } },
            });

            var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
                Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken));

            Assert.Contains("Could not resolve to a Repository", error.Message);
        }

        [Fact]
        public async Task GetHistoryAsync_FailedRequest_Throws()
        {
            var handler = new RecordingHandler((HttpStatusCode.Unauthorized, "{ \"message\": \"Bad credentials\" }"));

            var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
                Create(handler).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken));

            Assert.Contains("Bad credentials", error.Message);
        }

        [Fact]
        public async Task GetHistoryAsync_RepositoryNotInOwnerNameForm_Throws()
        {
            var handler = Handler(Payload(published: [], branches: []));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Create(handler, Options(repository: "vc-content")).GetHistoryAsync(Path, Query(), TestContext.Current.CancellationToken));

            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task GetHistoryAsync_WithoutABaseBranch_Throws()
        {
            var handler = Handler(Payload(published: [], branches: []));

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                Create(handler).GetHistoryAsync(Path, new GitHistoryQuery(), TestContext.Current.CancellationToken));
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static GitHistoryQuery Query() => new() { BaseBranch = "master" };

        private static IOptions<GitContentOptions> Options(string repository = "o/r", Action<GitContentOptions> configure = null)
        {
            var options = new GitContentOptions
            {
                Repository = repository,
                BaseBranch = "master",
                GraphQlUrl = new Uri("https://api.github.test/graphql"),
            };
            configure?.Invoke(options);

            return Microsoft.Extensions.Options.Options.Create(options);
        }

        private static GitHubContentHistory Create(RecordingHandler handler, IOptions<GitContentOptions> options = null) =>
            new(new FakeHttpClientFactory(handler), new MemoryCache(new MemoryCacheOptions()), options ?? Options());

        private static RecordingHandler Handler(params JObject[] payloads) =>
            new(payloads.Select(payload => (HttpStatusCode.OK, payload.ToString())).ToArray());

        private static JObject Payload(JObject[] published, (string Branch, JObject[] Commits)[] branches, bool hasNextPage = false, string endCursor = null)
        {
            return new JObject
            {
                ["data"] = new JObject
                {
                    ["repository"] = new JObject
                    {
                        ["published"] = new JObject { ["target"] = History(published) },
                        ["branches"] = new JObject
                        {
                            ["pageInfo"] = new JObject { ["hasNextPage"] = hasNextPage, ["endCursor"] = endCursor },
                            ["nodes"] = new JArray(branches.Select(branch => new JObject
                            {
                                ["name"] = branch.Branch,
                                ["target"] = History(branch.Commits),
                            }).Cast<object>().ToArray()),
                        },
                    },
                },
            };
        }

        private static JObject History(IEnumerable<JObject> commits) =>
            new() { ["history"] = new JObject { ["nodes"] = new JArray(commits.Cast<object>().ToArray()) } };

        private static JObject Commit(string sha, string date, string headline, int? files = 1) => new()
        {
            ["oid"] = sha,
            ["committedDate"] = date,
            ["messageHeadline"] = headline,
            ["messageBody"] = string.Empty,
            ["changedFilesIfAvailable"] = files,
            ["author"] = Author("Editor One", "editor@example.com", login: null),
        };

        private static JObject Author(string name, string email, string login) => new()
        {
            ["name"] = name,
            ["email"] = email,
            ["user"] = login == null ? null : new JObject { ["login"] = login },
        };

        private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) =>
                new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.github.test/") };
        }

        /// <summary>
        /// Answers with the queued responses in order (repeating the last one), and keeps the request
        /// bodies so a test can assert on what was actually asked of GitHub.
        /// </summary>
        private sealed class RecordingHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
        {
            private readonly List<string> _bodies = [];

            public int RequestCount => _bodies.Count;

            public string LastBody => _bodies[^1];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://api.github.test/graphql", request.RequestUri!.ToString());

                _bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

                var (status, body) = responses[Math.Min(_bodies.Count - 1, responses.Length - 1)];
                return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }
        }
    }
}
