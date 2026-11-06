using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// A database page's version history in its site's repository: each change committed to the base branch,
/// listed newest first, and the published one told by its content.
/// </summary>
public class GroupedPageHistoryTests
{
    private const string Live = """{ "v": 2 }""";

    [Fact]
    public void A_file_is_named_the_way_git_names_it()
    {
        // git hash-object of an empty file, and of the Pro Git book's "test content\n"
        Assert.Equal("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391", GitHubFileHistoryService.ComputeBlobOid(""));
        Assert.Equal("d670460b4b4aece5915caf5c68d12f560a9fe3e4", GitHubFileHistoryService.ComputeBlobOid("test content\n"));
        Assert.Null(GitHubFileHistoryService.ComputeBlobOid(null));
    }

    [Fact]
    public async Task The_newest_change_holding_the_live_content_is_the_published_one()
    {
        var github = new GitHubScript(("POST", "graphql", (request, body) =>
        {
            Assert.True(request.Options.TryGetValue(GitHubAuthHandler.OwnerOptionKey, out var owner));
            Assert.Equal("acme", owner);
            var variables = JObject.Parse(body!)["variables"]!;
            Assert.Equal("site-s1", variables["name"]?.Value<string>());
            Assert.Equal("refs/heads/main", variables["branch"]?.Value<string>());
            Assert.Equal("database-pages/g1.json", variables["path"]?.Value<string>());
            Assert.Equal("cursor-1", variables["after"]?.Value<string>());
            return GitHubScript.Json(Answer(hasMore: true, ("c4", """{ "v": 3 }"""), ("c3", Live), ("c2", """{ "v": 1 }"""), ("c1", Live)).ToString());
        }));

        var history = await History(github, published: Live).GetHistoryAsync(Group(), "cursor-1", cancellationToken: TestContext.Current.CancellationToken);

        github.AssertDone();
        Assert.Equal(["c4", "c3", "c2", "c1"], history!.Versions.Select(x => x.Sha));
        // published, edited, then discarded back: only the newest of the two is live
        Assert.Equal([false, true, false, false], history.Versions.Select(x => x.Published));
        Assert.Equal("alice", history.Versions[0].VcUser);
        // the page goes without saying in its own history
        Assert.Equal("Saved", history.Versions[0].Message);
        Assert.True(history.HasMore);
        Assert.Equal("cursor-2", history.EndCursor);
    }

    [Fact]
    public async Task A_page_never_published_has_no_published_change()
    {
        var github = new GitHubScript(("POST", "graphql", (_, _) => GitHubScript.Json(Answer(hasMore: false, ("c1", Live)).ToString())));

        var history = await History(github, published: null).GetHistoryAsync(Group(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(history!.Versions.Single().Published);
        Assert.False(history.HasMore);
    }

    [Fact]
    public async Task A_save_becomes_a_change_on_the_base_branch_saying_who_made_it()
    {
        var github = new GitHubScript(
            ("GET", "repos/acme/site-s1/contents/database-pages/g1.json?ref=main", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
            ("PUT", "repos/acme/site-s1/contents/database-pages/g1.json", (_, body) =>
            {
                var commit = JObject.Parse(body!);
                Assert.Equal("main", commit["branch"]?.Value<string>());
                Assert.StartsWith("Saved: \"Home\" (/home)", commit["message"]?.Value<string>());
                Assert.EndsWith("VC-User: alice", commit["message"]?.Value<string>());
                Assert.Equal("alice@acme.test", commit["author"]?["email"]?.Value<string>());
                // the exact bytes the database holds: what tells the published change later
                Assert.Equal(Live, Encoding.UTF8.GetString(Convert.FromBase64String(commit["content"]!.Value<string>()!)));
                return GitHubScript.Json("""{ "commit": { "sha": "c9" } }""", HttpStatusCode.Created);
            }));

        await History(github, published: null).RecordAsync(Group(), Live, "Saved", Alice(), null, TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task A_store_without_history_records_nothing_and_has_none()
    {
        var github = new GitHubScript();
        var history = History(github, published: Live, enabled: false);

        await history.RecordAsync(Group(), Live, "Save", Alice(), null, TestContext.Current.CancellationToken);

        Assert.Null(await history.GetHistoryAsync(Group(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, github.Requests);
    }

    [Fact]
    public async Task GitHub_failing_costs_the_history_a_change_never_the_save()
    {
        var github = new GitHubScript(("GET", "repos/acme/site-s1/contents/database-pages/g1.json?ref=main", (_, _) => GitHubScript.Status(HttpStatusCode.BadGateway)));

        await History(github, published: null).RecordAsync(Group(), Live, "Save", Alice(), null, TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    /// <summary>A repository assigned by hand keeps the git flow it had: no history of database pages goes into it.</summary>
    [Fact]
    public async Task A_store_with_a_repository_assigned_by_hand_keeps_no_history()
    {
        var github = new GitHubScript();
        var ownership = new FakeGitContentOwnership();
        var history = History(github, published: Live, assignedRepository: "acme/website", ownership: ownership);

        await history.RecordAsync(Group(), Live, "Save", Alice(), null, TestContext.Current.CancellationToken);

        Assert.Null(await history.GetHistoryAsync(Group(), null, 20, TestContext.Current.CancellationToken));
        Assert.Equal(0, github.Requests);
        Assert.Empty(ownership.Asked);
    }

    [Fact]
    public async Task GitHub_timing_out_costs_the_history_a_change_never_the_save()
    {
        // HttpClient.Timeout ends a request with a TaskCanceledException, not an HttpRequestException
        var github = new GitHubScript(("GET", "repos/acme/site-s1/contents/database-pages/g1.json?ref=main", (_, _) => throw new TaskCanceledException()));

        await History(github, published: null).RecordAsync(Group(), Live, "Save", Alice(), null, TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task A_save_the_editor_cancels_is_not_recorded_as_a_missed_change()
    {
        using var cancelled = new CancellationTokenSource();
        var github = new GitHubScript(("GET", "repos/acme/site-s1/contents/database-pages/g1.json?ref=main", (_, _) =>
        {
            cancelled.Cancel();
            throw new TaskCanceledException();
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => History(github, published: null).RecordAsync(Group(), Live, "Save", Alice(), null, cancelled.Token));
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private static GroupedPageBuilderPage Group() => new()
    {
        Id = "g1",
        StoreId = "s1",
        Name = "Home",
        Permalink = "/home",
        Pages = [new PageBuilderPage { Id = "p1", Status = Published }],
    };

    private static ClaimsPrincipal Alice() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice"), new Claim(ClaimTypes.Email, "alice@acme.test")], "test"));

    private static JObject Answer(bool hasMore, params (string Sha, string Content)[] commits) => new()
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
                            ["pageInfo"] = new JObject { ["hasNextPage"] = hasMore, ["endCursor"] = "cursor-2" },
                            ["nodes"] = new JArray(commits.Select(commit => new JObject
                            {
                                ["oid"] = commit.Sha,
                                ["committedDate"] = "2026-10-16T12:00:00Z",
                                ["messageHeadline"] = "Saved: \"Home\" (/home)",
                                ["messageBody"] = "VC-User: alice",
                                ["author"] = new JObject { ["name"] = "alice", ["email"] = "alice@acme.test" },
                                ["file"] = new JObject { ["oid"] = GitHubFileHistoryService.ComputeBlobOid(commit.Content) },
                            })),
                        },
                    },
                },
            },
        },
    };

    [Fact]
    public async Task Of_two_published_pages_the_newest_is_the_live_one_as_for_publishing()
    {
        var group = new GroupedPageBuilderPage
        {
            Id = "g1",
            StoreId = "s1",
            Pages =
            [
                new PageBuilderPage { Id = "older", Status = Published, ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new PageBuilderPage { Id = "newer", Status = Published, ModifiedDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            ],
        };
        var options = Options.Create(new GitContentOptions());
        var history = new GroupedPageHistoryService(null, null, null, null, new PageContents(), null, options, NullLogger<GroupedPageHistoryService>.Instance);

        Assert.Equal("content of newer", await history.GetPublishedContentAsync(group, TestContext.Current.CancellationToken));
    }

    private static GroupedPageHistoryService History(GitHubScript github, string published, bool enabled = true, string assignedRepository = null,
        FakeGitContentOwnership ownership = null)
    {
        var options = Options.Create(new GitContentOptions
        {
            Enabled = true,
            Organization = "acme",
            Token = "t0ken",
            BaseBranch = "main",
            GraphQlUrl = new Uri("https://api.github.test/graphql"),
        });
        var policy = GitHubConnections.Policy(options, new FakeSettingsManager(enabled, assignedRepository), ownership ?? new FakeGitContentOwnership());
        var repository = new GitHubContentRepository(github, new MemoryCache(new MemoryCacheOptions()), options);
        return new PublishedContent(policy, repository, github, options, published);
    }

    /// <summary>Each page's content names the page; nothing else is supported.</summary>
    private sealed class PageContents : IGroupedPageService
    {
        public Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default) => Task.FromResult($"content of {pageId}");

        public Task<IList<GroupedPageBuilderPage>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true) => throw new NotSupportedException();
        public Task SaveChangesAsync(IList<GroupedPageBuilderPage> models) => throw new NotSupportedException();
        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
        public Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryDeleteEmptyDraftAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>The history with the page's live content given, rather than read from the database.</summary>
    private sealed class PublishedContent(IGitContentPolicy policy, IGitContentRepository repository, GitHubScript github, IOptions<GitContentOptions> options, string published)
        : GroupedPageHistoryService(policy, repository, new GitHubApiClient(github), GitHubConnections.Service(options.Value), null, null, options,
            NullLogger<GroupedPageHistoryService>.Instance)
    {
        public override Task<string> GetPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default) =>
            Task.FromResult(published);
    }
}
