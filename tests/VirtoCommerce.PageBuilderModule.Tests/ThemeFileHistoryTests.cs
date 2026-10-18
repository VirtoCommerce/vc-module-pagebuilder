using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// A theme file's version history in its site's repository: each save, restore and discard committed to the base
    /// branch under "theme/", with the bytes the theme storage holds, and the published change told by its content.
    /// </summary>
    public class ThemeFileHistoryTests
    {
        private const string Path = "default/templates/product.json";
        private const string RepoPath = "theme/default/templates/product.json";
        private const string Sha = "1c0ffee1c0ffee1c0ffee1c0ffee1c0ffee1c0ff";
        // a date-like string and a layout of its own: what re-serializing would change
        private const string Old = "{\n  \"settings\": { \"since\": \"2026-10-16T12:00:00Z\" },\n  \"content\": []\n}";

        [Fact]
        public async Task A_saved_theme_file_becomes_a_change_under_theme_with_the_bytes_written()
        {
            var live = new FakeLiveStorage();
            string committed = null;
            var github = new GitHubScript(
                ("GET", $"repos/acme/site-s1/contents/{RepoPath}?ref=main", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
                ("PUT", $"repos/acme/site-s1/contents/{RepoPath}", (_, body) =>
                {
                    var commit = JObject.Parse(body!);
                    Assert.Equal("main", commit["branch"]?.Value<string>());
                    Assert.StartsWith($"Saved: {Path}", commit["message"]?.Value<string>());
                    Assert.EndsWith("VC-User: alice", commit["message"]?.Value<string>());
                    committed = Encoding.UTF8.GetString(Convert.FromBase64String(commit["content"]!.Value<string>()!));
                    return GitHubScript.Json("""{ "commit": { "sha": "c9" } }""", HttpStatusCode.Created);
                }));
            var files = new[] { new { path = Path, type = "themes", content = new { settings = new { }, content = new[] { new { id = "a", type = "product-info" } } } } };

            await Builder(github, live).SaveTemplates("s1", "default", new PageBuilderController.SaveFilesModel { Files = JsonConvert.SerializeObject(files) }, draft: true);

            github.AssertDone();
            // exactly the draft's bytes: what tells the published change once it is published
            Assert.Equal(live.Files[$"{Path}-draft"], committed);
        }

        [Fact]
        public async Task The_newest_change_holding_the_live_file_is_the_published_one()
        {
            var live = new FakeLiveStorage();
            live.Files[Path] = Old;
            var github = new GitHubScript(("POST", "graphql", (_, body) =>
            {
                Assert.Equal(RepoPath, JObject.Parse(body!)["variables"]?["path"]?.Value<string>());
                return GitHubScript.Json(Answer(("c3", "{}"), ("c2", Old), ("c1", Old)).ToString());
            }));

            var result = await Controller(github, live).GetHistory("s1", Path, cancellationToken: TestContext.Current.CancellationToken);

            var history = Assert.IsType<GroupedPageHistoryResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(["c3", "c2", "c1"], history.Versions.Select(x => x.Sha));
            Assert.Equal([false, true, false], history.Versions.Select(x => x.Published));
            // the file goes without saying in its own history
            Assert.Equal("Saved", history.Versions[0].Message);
            Assert.Equal("save-0001", history.Versions[0].SaveId);
        }

        [Fact]
        public async Task Restoring_a_change_makes_its_exact_bytes_the_draft_and_a_change_on_top()
        {
            var live = new FakeLiveStorage();
            var github = new GitHubScript(
                ("GET", $"repos/acme/site-s1/contents/{RepoPath}?ref={Sha}", (_, _) => GitHubScript.Json(Contents(Old))),
                ("GET", $"repos/acme/site-s1/contents/{RepoPath}?ref=main", (_, _) => GitHubScript.Json(Contents("{}"))),
                ("PUT", $"repos/acme/site-s1/contents/{RepoPath}", (_, body) =>
                {
                    var commit = JObject.Parse(body!);
                    Assert.StartsWith($"Restored 1c0ffee: {Path}", commit["message"]?.Value<string>());
                    Assert.Equal(Old, Encoding.UTF8.GetString(Convert.FromBase64String(commit["content"]!.Value<string>()!)));
                    return GitHubScript.Json("""{ "commit": { "sha": "c9" } }""", HttpStatusCode.Created);
                }));

            Assert.IsType<OkResult>(await Controller(github, live).Restore(Sha, "s1", Path, cancellationToken: TestContext.Current.CancellationToken));

            github.AssertDone();
            // a draft: what is published stays, until Publish
            Assert.Equal(Old, live.Files[$"{Path}-draft"]);
            Assert.False(live.Files.ContainsKey(Path));
            Assert.Single(live.Events.OfType<PageBuilderContentChangedEvent>());
        }

        [Theory]
        [InlineData("save-1234abcd", "VC-User: alice\nVC-Save: save-1234abcd")]
        // not an id: it would end up in the commit message as it is
        [InlineData("x\nVC-User: mallory", "VC-User: alice")]
        [InlineData(null, "VC-User: alice")]
        public async Task Each_change_of_a_designer_save_carries_its_id(string header, string trailers)
        {
            var github = new GitHubScript(
                ("GET", $"repos/acme/site-s1/contents/{RepoPath}?ref=main", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
                ("PUT", $"repos/acme/site-s1/contents/{RepoPath}", (_, body) =>
                {
                    Assert.EndsWith($"{Path}\n\n{trailers}", JObject.Parse(body!)["message"]?.Value<string>());
                    return GitHubScript.Json("""{ "commit": { "sha": "c9" } }""", HttpStatusCode.Created);
                }));
            var builder = Builder(github, new FakeLiveStorage());
            builder.HttpContext.Request.Headers[SiteSave.Header] = header;
            var files = new[] { new { path = Path, type = "themes", content = new { content = Array.Empty<object>() } } };

            await builder.SaveTemplates("s1", "default", new PageBuilderController.SaveFilesModel { Files = JsonConvert.SerializeObject(files) }, draft: true);

            github.AssertDone();
        }

        [Fact]
        public async Task A_save_restored_as_a_whole_names_the_version_picked_in_each_file()
        {
            var github = new GitHubScript(
                ("GET", $"repos/acme/site-s1/contents/{RepoPath}?ref={Sha}", (_, _) => GitHubScript.Json(Contents(Old))),
                ("GET", $"repos/acme/site-s1/contents/{RepoPath}?ref=main", (_, _) => GitHubScript.Json(Contents("{}"))),
                ("PUT", $"repos/acme/site-s1/contents/{RepoPath}", (_, body) =>
                {
                    var message = JObject.Parse(body!)["message"]?.Value<string>();
                    // the save's version, not this file's own commit
                    Assert.StartsWith($"Restored abcdef1: {Path}", message);
                    Assert.EndsWith("VC-Save: restore-0001", message);
                    return GitHubScript.Json("""{ "commit": { "sha": "c9" } }""", HttpStatusCode.Created);
                }));
            var controller = Controller(github, new FakeLiveStorage());
            controller.HttpContext.Request.Headers[SiteSave.Header] = "restore-0001";

            Assert.IsType<OkResult>(await controller.Restore(Sha, "s1", Path, "abcdef1234", TestContext.Current.CancellationToken));

            github.AssertDone();
        }

        [Fact]
        public void A_trailer_joins_the_ones_a_message_ends_with()
        {
            Assert.Equal("Saved: x\n\nVC-Save: s1", GitCommitMessage.WithTrailer("Saved: x", "VC-Save", "s1"));
            Assert.Equal("Saved: x\n\nVC-User: alice\nVC-Save: s1", GitCommitMessage.WithTrailer("Saved: x\n\nVC-User: alice", "VC-Save", "s1"));
            Assert.Equal("Saved: x", GitCommitMessage.WithTrailer("Saved: x", "VC-Save", " "));
            Assert.Equal("s1", GitCommitMessage.ReadTrailer("VC-User: alice\nVC-Save: s1", GitCommitMessage.VcSaveTrailer));
        }

        [Fact]
        public async Task Discarding_a_draft_records_the_published_file_again()
        {
            var live = new FakeLiveStorage();
            live.Files[Path] = Old;
            live.Files[$"{Path}-draft"] = "{}";
            var github = new GitHubScript(
                ("GET", $"repos/acme/site-s1/contents/{RepoPath}?ref=main", (_, _) => GitHubScript.Json(Contents("{}"))),
                ("PUT", $"repos/acme/site-s1/contents/{RepoPath}", (_, body) =>
                {
                    var commit = JObject.Parse(body!);
                    Assert.StartsWith("Discarded unpublished changes", commit["message"]?.Value<string>());
                    Assert.Equal(Old, Encoding.UTF8.GetString(Convert.FromBase64String(commit["content"]!.Value<string>()!)));
                    return GitHubScript.Json("""{ "commit": { "sha": "c9" } }""", HttpStatusCode.Created);
                }));

            await Builder(github, live).DiscardDraft("s1", Path, "themes", "default");

            github.AssertDone();
        }

        [Theory]
        [InlineData("../pages/home.page")]
        [InlineData("default/../../x.json")]
        [InlineData("/default/templates/product.json")]
        [InlineData("default\\templates\\product.json")]
        [InlineData("")]
        public async Task A_path_leaving_the_theme_folder_has_no_history(string path)
        {
            var github = new GitHubScript();

            Assert.False(ThemeFileHistory.IsThemePath(path));
            await History(github).RecordAsync("s1", path, "{}", "Saved", Alice(), null, TestContext.Current.CancellationToken);
            Assert.IsType<NotFoundResult>((await Controller(github, new FakeLiveStorage()).GetHistory("s1", path, cancellationToken: TestContext.Current.CancellationToken)).Result);

            Assert.Equal(0, github.Requests);
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static ClaimsPrincipal Alice() =>
            new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice"), new Claim(ClaimTypes.Email, "alice@acme.test")], "test"));

        private static IOptions<GitContentOptions> GitOptions() => Options.Create(new GitContentOptions
        {
            Enabled = true,
            Organization = "acme",
            Token = "t0ken",
            BaseBranch = "main",
            GraphQlUrl = new Uri("https://api.github.test/graphql"),
        });

        private static GitContentPolicy Policy(IOptions<GitContentOptions> options) => new(options, new FakeSettingsManager(true));

        private static ThemeFileHistory History(GitHubScript github)
        {
            var options = GitOptions();
            var repository = new GitHubContentRepository(github, new MemoryCache(new MemoryCacheOptions()), options);
            return new ThemeFileHistory(Policy(options), repository, github, options, NullLogger<ThemeFileHistory>.Instance);
        }

        private static ThemeFileHistoryController Controller(GitHubScript github, FakeLiveStorage live) =>
            WithUser(new ThemeFileHistoryController(live, live, live, live, History(github)));

        private static PageBuilderController Builder(GitHubScript github, FakeLiveStorage live)
        {
            var options = GitOptions();
            return WithUser(new PageBuilderController(
                storeService: live.StoreService,
                pathResolver: live,
                blobContentStorageProviderFactory: live,
                publishingService: live,
                eventPublisher: live,
                gitContentOptions: options,
                gitContentPolicy: Policy(options),
                gitContentRepository: new GitHubContentRepository(github, new MemoryCache(new MemoryCacheOptions()), options),
                gitContentHistory: null,
                gitContentPublisher: null,
                gitContentProvisioner: new FakeGitContentProvisioner(),
                settingsManager: null,
                authorizationService: new FakeAuthorizationService(true),
                themeFileHistory: History(github)));
        }

        private static T WithUser<T>(T controller) where T : ControllerBase
        {
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Alice() } };
            return controller;
        }

        private static string Contents(string content) =>
            new JObject { ["sha"] = "b0", ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) }.ToString();

        private static JObject Answer(params (string Sha, string Content)[] commits) => new()
        {
            ["data"] = new JObject
            {
                ["repository"] = new JObject
                {
                    ["ref"] = new JObject
                    {
                        ["target"] = new JObject
                        {
                            ["history"] = new JObject
                            {
                                ["pageInfo"] = new JObject { ["hasNextPage"] = false, ["endCursor"] = null },
                                ["nodes"] = new JArray(commits.Select(commit => new JObject
                                {
                                    ["oid"] = commit.Sha,
                                    ["committedDate"] = "2026-10-18T12:00:00Z",
                                    ["messageHeadline"] = $"Saved: {Path}",
                                    ["messageBody"] = "VC-User: alice\nVC-Save: save-0001",
                                    ["author"] = new JObject { ["name"] = "alice", ["email"] = "alice@acme.test" },
                                    ["file"] = new JObject { ["oid"] = SiteHistory.BlobOid(commit.Content) },
                                })),
                            },
                        },
                    },
                },
            },
        };
    }
}
