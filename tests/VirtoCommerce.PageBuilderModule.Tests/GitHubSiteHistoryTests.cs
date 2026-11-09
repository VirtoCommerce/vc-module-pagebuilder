using System;
using System.Buffers.Text;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;
using Fakes = VirtoCommerce.PageBuilderModule.Tests.PublishedRenameContentPreservationTests;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GitHubSiteHistoryTests
{
    private const string Header = "default/sections/header-group.json";
    private const string Rules = "default/config/template_rules.json";
    private const string LivePage = """{ "settings": {}, "content": [] }""";
    private const string LiveHeader = """{ "content": [] }""";

    private static readonly HistoryItem Page = new(HistoryItemType.Page, "g1");
    private static readonly HistoryItem HeaderItem = new(HistoryItemType.Theme, Header);

    private readonly FakeBlobStorage _live = new();
    private readonly Fakes.FakeGroupedPageService _pages = new();

    public GitHubSiteHistoryTests()
    {
        _pages.SeedGroup(new GroupedPageBuilderPage { Id = "g1", StoreId = "s1", Pages = [new PageBuilderPage { Id = "p1", Status = Published }] });
        _pages.SeedContent("p1", LivePage);
        _live.Files[Header] = LiveHeader;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ComputeBlobOid_Content_ReturnsGitBlobHash()
    {
        // git hash-object of an empty file, and of the Pro Git book's "test content\n"
        Assert.Equal("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391", GitHubSiteHistory.ComputeBlobOid(""));
        Assert.Equal("d670460b4b4aece5915caf5c68d12f560a9fe3e4", GitHubSiteHistory.ComputeBlobOid("test content\n"));
        Assert.Null(GitHubSiteHistory.ComputeBlobOid(null));
    }

    [Fact]
    public async Task RecordAsync_SeveralEntries_CommitsThemAsOneCommit()
    {
        var github = new GitHubScript(
            HeadOfMain("1111111111111111111111111111111111111111"),
            ("GET", "repos/acme/site-s1/git/commits/1111111111111111111111111111111111111111", (_, _) => GitHubScript.Json("""{ "tree": { "sha": "t0" } }""")),
            ("POST", "repos/acme/site-s1/git/trees", (_, body) =>
            {
                var tree = JObject.Parse(body!);
                Assert.Equal("t0", tree["base_tree"]?.Value<string>());
                Assert.Equal(
                    [("database-pages/g1.json", LivePage), ($"theme/{Header}", LiveHeader), ($"theme/{Rules}", "{}")],
                    tree["tree"]!.Select(x => (x["path"]!.Value<string>(), x["content"]!.Value<string>())));
                Assert.All(tree["tree"]!, x => Assert.Equal("100644", x["mode"]?.Value<string>()));
                return GitHubScript.Json("""{ "sha": "t1" }""", HttpStatusCode.Created);
            }),
            ("POST", "repos/acme/site-s1/git/commits", (_, body) =>
            {
                var commit = JObject.Parse(body!);
                Assert.Equal("t1", commit["tree"]?.Value<string>());
                Assert.Equal(["1111111111111111111111111111111111111111"], commit["parents"]!.Values<string>());
                Assert.Equal(
                    $"Saved\n\ndatabase-pages/g1.json\ntheme/{Rules}\ntheme/{Header}\n\nVC-User: alice\nVC-Save: save-0001",
                    commit["message"]?.Value<string>());
                Assert.Equal("alice@acme.test", commit["author"]?["email"]?.Value<string>());
                return GitHubScript.Json("""{ "sha": "2222222222222222222222222222222222222222" }""", HttpStatusCode.Created);
            }),
            ("PATCH", "repos/acme/site-s1/git/refs/heads/main", (_, body) =>
            {
                Assert.Equal("2222222222222222222222222222222222222222", JObject.Parse(body!)["sha"]?.Value<string>());
                Assert.False(JObject.Parse(body!)["force"]!.Value<bool>());
                return GitHubScript.Json("{}");
            }));

        await History(github).RecordAsync("s1", Record(("page:g1", LivePage), ($"theme:{Header}", LiveHeader), ($"theme:{Rules}", "{}")), Ct);

        github.AssertDone();
    }

    [Fact]
    public async Task RecordAsync_BranchMovedMeanwhile_CommitsAgainOnItsNewHead()
    {
        var github = new GitHubScript(
            HeadOfMain("1111111111111111111111111111111111111111"),
            ("GET", "repos/acme/site-s1/git/commits/1111111111111111111111111111111111111111", (_, _) => GitHubScript.Json("""{ "tree": { "sha": "t0" } }""")),
            ("POST", "repos/acme/site-s1/git/trees", (_, _) => GitHubScript.Json("""{ "sha": "t1" }""", HttpStatusCode.Created)),
            ("POST", "repos/acme/site-s1/git/commits", (_, _) => GitHubScript.Json("""{ "sha": "c1" }""", HttpStatusCode.Created)),
            ("PATCH", "repos/acme/site-s1/git/refs/heads/main", (_, _) => GitHubScript.Json("""{ "message": "Update is not a fast forward" }""", HttpStatusCode.UnprocessableEntity)),
            HeadOfMain("3333333333333333333333333333333333333333"),
            ("GET", "repos/acme/site-s1/git/commits/3333333333333333333333333333333333333333", (_, _) => GitHubScript.Json("""{ "tree": { "sha": "t3" } }""")),
            ("POST", "repos/acme/site-s1/git/trees", (_, body) =>
            {
                Assert.Equal("t3", JObject.Parse(body!)["base_tree"]?.Value<string>());
                return GitHubScript.Json("""{ "sha": "t4" }""", HttpStatusCode.Created);
            }),
            ("POST", "repos/acme/site-s1/git/commits", (_, body) =>
            {
                Assert.Equal(["3333333333333333333333333333333333333333"], JObject.Parse(body!)["parents"]!.Values<string>());
                return GitHubScript.Json("""{ "sha": "c4" }""", HttpStatusCode.Created);
            }),
            ("PATCH", "repos/acme/site-s1/git/refs/heads/main", (_, _) => GitHubScript.Json("{}")));

        await History(github).RecordAsync("s1", Record(($"theme:{Header}", LiveHeader)), Ct);

        github.AssertDone();
    }

    [Fact]
    public async Task RecordAsync_NothingChanged_MakesNoCommit()
    {
        var github = new GitHubScript(
            HeadOfMain("1111111111111111111111111111111111111111"),
            ("GET", "repos/acme/site-s1/git/commits/1111111111111111111111111111111111111111", (_, _) => GitHubScript.Json("""{ "tree": { "sha": "t0" } }""")),
            ("POST", "repos/acme/site-s1/git/trees", (_, _) => GitHubScript.Json("""{ "sha": "t0" }""", HttpStatusCode.Created)));

        await History(github).RecordAsync("s1", Record(($"theme:{Header}", LiveHeader)), Ct);

        github.AssertDone();
    }

    [Fact]
    public async Task RecordAsync_RepositoryNotCreatedYet_CreatesItAndRecordsTheVersion()
    {
        var provisioner = new FakeGitContentProvisioner();
        var github = new GitHubScript(
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => GitHubScript.Json("""{ "message": "Not Found" }""", HttpStatusCode.NotFound)),
            HeadOfMain("1111111111111111111111111111111111111111"),
            ("GET", "repos/acme/site-s1/git/commits/1111111111111111111111111111111111111111", (_, _) => GitHubScript.Json("""{ "tree": { "sha": "t0" } }""")),
            ("POST", "repos/acme/site-s1/git/trees", (_, _) => GitHubScript.Json("""{ "sha": "t1" }""", HttpStatusCode.Created)),
            ("POST", "repos/acme/site-s1/git/commits", (_, _) => GitHubScript.Json("""{ "sha": "c1" }""", HttpStatusCode.Created)),
            ("PATCH", "repos/acme/site-s1/git/refs/heads/main", (_, _) => GitHubScript.Json("""{ "object": { "sha": "c1" } }""")));

        await History(github, provisioner: provisioner).RecordAsync("s1", Record(($"theme:{Header}", LiveHeader)), Ct);

        Assert.Equal(["s1"], provisioner.Ensured);
        github.AssertDone();
    }

    [Theory]
    [InlineData("theme:default/snapshots/1/en-US.json")]
    [InlineData("theme:../x.json")]
    [InlineData("page:a/b")]
    public async Task RecordAsync_ItemTheHistoryDoesNotKeep_CallsNoGitHub(string item)
    {
        var github = new GitHubScript();

        await History(github).RecordAsync("s1", Record((item, "{}")), Ct);

        Assert.Equal(0, github.Requests);
    }

    [Fact]
    public async Task RecordAsync_GitHubFails_DoesNotThrow()
    {
        var github = new GitHubScript(("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => GitHubScript.Status(HttpStatusCode.BadGateway)));

        await History(github).RecordAsync("s1", Record(($"theme:{Header}", LiveHeader)), Ct);

        github.AssertDone();
    }

    [Fact]
    public async Task RecordAsync_CallerCancels_ThrowsOperationCanceledException()
    {
        using var cancelled = new CancellationTokenSource();
        var github = new GitHubScript(("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) =>
        {
            cancelled.Cancel();
            throw new TaskCanceledException();
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => History(github).RecordAsync("s1", Record(($"theme:{Header}", "{}")), cancelled.Token));
    }

    [Theory]
    [InlineData(true, null, ContentHistoryStatus.Enabled)]
    // the store's repository is not created yet: git content is off for the store
    [InlineData(false, null, ContentHistoryStatus.SetupRequired)]
    [InlineData(true, "acme/website", ContentHistoryStatus.Unavailable)]
    [InlineData(true, "not a repository", ContentHistoryStatus.Unavailable)]
    public async Task GetStatusAsync_Store_ReturnsWhetherHistoryIsKept(bool gitContent, string assignedRepository, ContentHistoryStatus expected)
    {
        var history = History(new GitHubScript(), new FakeSettingsManager(gitContent, assignedRepository));

        Assert.Equal(expected, await history.GetStatusAsync("s1", Ct));
    }

    [Fact]
    public async Task GetStatusAsync_GitHubNotConnected_ReturnsSetupRequired()
    {
        var options = SiteHistories.Options();
        options.Value.Organization = null;
        options.Value.Token = null;

        Assert.Equal(ContentHistoryStatus.SetupRequired, await History(new GitHubScript(), options: options).GetStatusAsync("s1", Ct));
    }

    [Theory]
    [InlineData(ContentHistoryStatus.SetupRequired, false, null)]
    [InlineData(ContentHistoryStatus.Unavailable, true, "acme/website")]
    public async Task ListAsync_StoreWithoutHistory_ReturnsItsStatusWithoutCallingGitHub(ContentHistoryStatus status, bool gitContent, string assignedRepository)
    {
        var github = new GitHubScript();

        var page = await History(github, new FakeSettingsManager(gitContent, assignedRepository)).ListAsync("s1", [Page], null, 20, Ct);

        Assert.Equal(status, page.Status);
        Assert.Empty(page.Versions);
        Assert.Null(page.Cursor);
        Assert.Equal(0, github.Requests);
    }

    [Fact]
    public async Task ListAsync_SeveralItems_ListsEachCommitOnceWithTheItemsItChanged()
    {
        var github = new GitHubScript(("POST", "graphql", (request, body) =>
        {
            Assert.True(request.Options.TryGetValue(GitHubAuthHandler.OwnerOptionKey, out var owner));
            Assert.Equal("acme", owner);
            var variables = JObject.Parse(body!)["variables"]!;
            Assert.Equal("site-s1", variables["name"]?.Value<string>());
            Assert.Equal("refs/heads/main", variables["branch"]?.Value<string>());
            Assert.Equal("database-pages/g1.json", variables["p0"]?.Value<string>());
            Assert.Equal($"theme/{Header}", variables["p1"]?.Value<string>());
            Assert.Equal(20, variables["t0"]?.Value<int>());
            Assert.Equal(JTokenType.Null, variables["a1"]?.Type);
            return Answer(
                (false, "e0", [(Sha('3'), 30, "{}"), (Sha('1'), 10, LivePage)]),
                (false, "e1", [(Sha('3'), 30, "{}"), (Sha('2'), 20, LiveHeader)]));
        }));

        var page = await History(github).ListAsync("s1", [Page, HeaderItem], null, 20, Ct);

        github.AssertDone();
        Assert.Equal(ContentHistoryStatus.Enabled, page.Status);
        Assert.Equal([VersionId(Sha('3')), VersionId(Sha('2')), VersionId(Sha('1'))], page.Versions.Select(x => x.VersionId));
        Assert.Equal(["page:g1", $"theme:{Header}"], page.Versions[0].Items);
        Assert.Equal([$"theme:{Header}"], page.Versions[1].Items);
        // the newest commit holding what is live, for every item it changed
        Assert.Equal([false, true, true], page.Versions.Select(x => x.Published));
        Assert.Equal("Saved", page.Versions[0].Description);
        Assert.Equal("save-0001", page.Versions[0].SaveId);
        Assert.Equal("alice", page.Versions[0].Author.Name);
        Assert.Equal("alice-gh", page.Versions[0].Author.Login);
        Assert.Equal(new DateTime(2026, 10, 18, 10, 30, 0, DateTimeKind.Utc), page.Versions[0].Date);
        Assert.Null(page.Cursor);
    }

    [Fact]
    public async Task ListAsync_MoreThanTake_ContinuesFromTheCursorWhereEachFileStopped()
    {
        var first = new GitHubScript(("POST", "graphql", (_, _) => Answer(
            (true, "e0", [(Sha('9'), 50, "{}"), (Sha('2'), 20, "{}")]),
            (true, "e1", [(Sha('8'), 40, "{}"), (Sha('7'), 35, "{}")]))));

        var page = await History(first).ListAsync("s1", [Page, HeaderItem], null, 2, Ct);

        Assert.Equal([VersionId(Sha('9')), VersionId(Sha('8'))], page.Versions.Select(x => x.VersionId));
        Assert.NotNull(page.Cursor);

        var second = new GitHubScript(("POST", "graphql", (_, body) =>
        {
            var request = JObject.Parse(body!);
            var variables = request["variables"]!;
            // the commit the first page read, not the branch: a save made since must not shift the positions
            Assert.Contains("object(oid:$head)", request.Value<string>("query"));
            Assert.Equal(FirstPageHead, variables["head"]?.Value<string>());
            Assert.Null(variables["branch"]);
            // each file read one of the two: the next request skips it
            Assert.Equal(3, variables["t0"]?.Value<int>());
            Assert.Equal(3, variables["t1"]?.Value<int>());
            Assert.Equal(JTokenType.Null, variables["a0"]?.Type);
            return AnchoredAnswer(
                (false, "e0", [(Sha('9'), 50, "{}"), (Sha('2'), 20, "{}"), (Sha('1'), 10, "{}")]),
                (false, "e1", [(Sha('8'), 40, "{}"), (Sha('7'), 35, "{}"), (Sha('6'), 30, "{}")]));
        }));

        var next = await History(second).ListAsync("s1", [Page, HeaderItem], page.Cursor, 2, Ct);

        Assert.Equal([VersionId(Sha('7')), VersionId(Sha('6'))], next.Versions.Select(x => x.VersionId));

        var third = new GitHubScript(("POST", "graphql", (_, body) =>
        {
            var variables = JObject.Parse(body!)["variables"]!;
            // the header's history is read to its end: only the page's is asked for
            Assert.Equal("database-pages/g1.json", variables["p0"]?.Value<string>());
            Assert.Null(variables["p1"]);
            Assert.Equal(3, variables["t0"]?.Value<int>());
            Assert.Equal(FirstPageHead, variables["head"]?.Value<string>());
            return AnchoredAnswer((false, "e0", [(Sha('9'), 50, "{}"), (Sha('2'), 20, "{}"), (Sha('1'), 10, "{}")]));
        }));

        var last = await History(third).ListAsync("s1", [Page, HeaderItem], next.Cursor, 2, Ct);

        Assert.Equal([VersionId(Sha('2')), VersionId(Sha('1'))], last.Versions.Select(x => x.VersionId));
        Assert.Null(last.Cursor);
    }

    [Fact]
    public async Task ListAsync_RepositoryNotCreatedYet_ReturnsSetupRequired()
    {
        var github = new GitHubScript(("POST", "graphql", (_, _) => GitHubScript.Json("""
            {
              "data": { "repository": null },
              "errors": [{
                "type": "NOT_FOUND",
                "path": ["repository"],
                "locations": [{ "line": 1, "column": 2 }],
                "message": "Could not resolve to a Repository with the name 'acme/site-s1'."
              }]
            }
            """)));

        var page = await History(github).ListAsync("s1", [Page], null, 20, Ct);

        Assert.Equal(ContentHistoryStatus.SetupRequired, page.Status);
        Assert.Empty(page.Versions);
    }

    [Fact]
    public async Task ListAsync_MostItemsWithLongPaths_WritesACursorTheRequestAccepts()
    {
        // twenty theme files with long names, each with more history than one page shows: the cursor does not grow with the paths
        var items = Enumerable.Range(0, 20)
            .Select(i => new HistoryItem(HistoryItemType.Theme, $"b2b-storefront-theme/templates/{new string('p', 250)}-{i:D2}.json"))
            .ToList();
        var github = new GitHubScript(("POST", "graphql", (_, _) => Answer(items
            .Select((_, i) => (true, $"{HexSha(i, 1)} 0", new[] { (HexSha(i, 1), 50 - i, "{}"), (HexSha(i, 2), 10, "{}") }))
            .ToArray())));

        var page = await History(github).ListAsync("s1", items, null, 1, Ct);

        var request = new ContentHistoryRequest { StoreId = "s1", Items = items.Select(x => x.ToString()).ToList(), Cursor = page.Cursor };
        var result = await RequestValidators.History("s1").ValidateAsync(request, Ct);
        Assert.True(result.IsValid, $"a cursor of {page.Cursor.Length} characters: {result}");
    }

    private static string HexSha(int item, int commit) => $"{item:x2}{commit:x2}".PadRight(40, '0');

    [Fact]
    public async Task ListAsync_GitHubFails_ThrowsGitContentException()
    {
        var github = new GitHubScript(("POST", "graphql", (_, _) => GitHubScript.Status(HttpStatusCode.BadGateway)));

        await Assert.ThrowsAsync<GitContentException>(() => History(github).ListAsync("s1", [Page], null, 20, Ct));
    }

    [Theory]
    [InlineData("bm90IGEgY3Vyc29y")]
    [InlineData("!!")]
    // well-formed positions the history did not write: a null one, and one before the start
    [InlineData("eyJ4IjpudWxsfQ")]
    [InlineData("eyJkYXRhYmFzZS1wYWdlcy9nMS5qc29uIjp7InMiOi0yMDB9fQ")]
    public async Task ListAsync_CursorItDidNotWrite_ThrowsArgumentException(string cursor)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => History(new GitHubScript()).ListAsync("s1", [Page], cursor, 20, Ct));
    }

    [Fact]
    public async Task ReadAsync_VersionId_ReadsTheItemAtItsCommit()
    {
        var github = new GitHubScript(($"GET", $"repos/acme/site-s1/contents/theme/{Header}?ref={Sha('4')}", (_, _) => GitHubScript.Json(Contents(LiveHeader))));

        Assert.Equal(LiveHeader, await History(github).ReadAsync("s1", HeaderItem, VersionId(Sha('4')), Ct));
        github.AssertDone();
    }

    [Theory]
    [InlineData("4444444444444444444444444444444444444444")]
    [InlineData("4444444")]
    [InlineData("REREREREREREREREREREREREREREREREREREREREREQ")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ReadAsync_NotAVersionId_ReturnsNullWithoutCallingGitHub(string versionId)
    {
        var github = new GitHubScript();

        Assert.Null(await History(github).ReadAsync("s1", HeaderItem, versionId, Ct));
        Assert.Equal(0, github.Requests);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private GitHubSiteHistory History(GitHubScript github, FakeSettingsManager settings = null,
        Microsoft.Extensions.Options.IOptions<GitContentOptions> options = null, IGitContentProvisioner provisioner = null) =>
        SiteHistories.Create(github, new ThemeStorageFactory(_live.StoreService, _live, _live, _live), _pages, settings, options: options, provisioner: provisioner);

    private static string Sha(char digit) => new(digit, 40);

    private static string VersionId(string sha) => Base64Url.EncodeToString(Convert.FromHexString(sha));

    private static (string, string, Func<System.Net.Http.HttpRequestMessage, string, System.Net.Http.HttpResponseMessage>) HeadOfMain(string sha) =>
        ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => GitHubScript.Json($$"""{ "object": { "sha": "{{sha}}" } }"""));

    private static ContentHistoryRecord Record(params (string Item, string Content)[] entries)
    {
        var record = new ContentHistoryRecord { Description = "Saved", SaveId = "save-0001", UserName = "alice", UserEmail = "alice@acme.test" };
        foreach (var (item, content) in entries)
        {
            Assert.True(HistoryItem.TryParse(item, out var historyItem));
            record.AddEntry(historyItem, content);
        }

        return record;
    }

    private static string Contents(string content) =>
        new JObject { ["sha"] = "b0", ["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)) }.ToString();

    private const string FirstPageHead = "f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0";

    // the branch head, as a first page reads it
    private static System.Net.Http.HttpResponseMessage Answer(params (bool HasMore, string EndCursor, (string Oid, int Minute, string Content)[] Nodes)[] histories) =>
        GitHubScript.Json(new JObject { ["data"] = new JObject { ["repository"] = new JObject { ["ref"] = new JObject { ["target"] = Commit(histories) } } } }.ToString());

    // the commit a first page read, as the next pages read it
    private static System.Net.Http.HttpResponseMessage AnchoredAnswer(params (bool HasMore, string EndCursor, (string Oid, int Minute, string Content)[] Nodes)[] histories) =>
        GitHubScript.Json(new JObject { ["data"] = new JObject { ["repository"] = new JObject { ["object"] = Commit(histories) } } }.ToString());

    private static JObject Commit((bool HasMore, string EndCursor, (string Oid, int Minute, string Content)[] Nodes)[] histories)
    {
        var target = new JObject { ["oid"] = FirstPageHead };
        for (var i = 0; i < histories.Length; i++)
        {
            var (hasMore, endCursor, nodes) = histories[i];
            target[$"h{i}"] = new JObject
            {
                ["pageInfo"] = new JObject { ["hasNextPage"] = hasMore, ["endCursor"] = endCursor },
                ["nodes"] = new JArray(nodes.Select(node => new JObject
                {
                    ["oid"] = node.Oid,
                    ["committedDate"] = $"2026-10-18T10:{node.Minute:D2}:00Z",
                    ["messageHeadline"] = "Saved",
                    ["messageBody"] = "database-pages/g1.json\n\nVC-User: alice\nVC-Save: save-0001",
                    ["author"] = new JObject { ["name"] = "Alice", ["email"] = "alice@acme.test", ["user"] = new JObject { ["login"] = "alice-gh" } },
                    ["file"] = new JObject { ["oid"] = GitHubSiteHistory.ComputeBlobOid(node.Content) },
                })),
            };
        }

        return target;
    }
}
