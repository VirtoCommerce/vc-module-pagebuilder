using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GitHubContentProvisionerTests
{
    private const string Repo = "acme/site-s1";
    private const string Sha = "1111111111111111111111111111111111111111";
    private static readonly string Mine = GitHubContentOwnership.DescriptionPrefix + "s1";

    [Fact]
    public async Task EnsureRepositoryAsync_MissingRepository_CreatesPrivateRepositoryMarkedAsStores()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
            ("POST", "orgs/acme/repos", (_, body) =>
            {
                var json = JObject.Parse(body);
                Assert.Equal("site-s1", json["name"]?.Value<string>());
                Assert.True(json["private"]?.Value<bool>());
                Assert.True(json["auto_init"]?.Value<bool>());
                Assert.Equal(Mine, json["description"]?.Value<string>());
                return GitHubScript.Json(RepoJson(Mine), HttpStatusCode.Created);
            }),
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()));

        await Provisioner(github).EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task EnsureRepositoryAsync_CreatedConcurrently_AdoptsIt()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
            ("POST", "orgs/acme/repos", (_, _) => GitHubScript.Json("""{ "message": "Repository creation failed.", "errors": [ { "message": "name already exists on this account" } ] }""", HttpStatusCode.UnprocessableEntity)),
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(Mine))),
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()));

        await Provisioner(github).EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task EnsureRepositoryAsync_ExistingRepository_MarksItAsStores()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson("Our site"))),
            ("PATCH", $"repos/{Repo}", (_, body) =>
            {
                Assert.Equal(Mine, JObject.Parse(body)["description"]?.Value<string>());
                return GitHubScript.Json("{}");
            }),
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()));
        var ownership = new FakeGitContentOwnership();

        await Provisioner(github, ownership: ownership).EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken);

        github.AssertDone();
        Assert.Equal([Repo], ownership.Forgotten);
    }

    [Fact]
    public async Task EnsureRepositoryAsync_ArchivedRepositoryOfStore_UnarchivesIt()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(Mine, archived: true))),
            ("PATCH", $"repos/{Repo}", (_, body) =>
            {
                Assert.False(JObject.Parse(body)["archived"]?.Value<bool>());
                return GitHubScript.Json("{}");
            }),
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()));

        await Provisioner(github).EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task EnsureRepositoryAsync_RepositoryOfAnotherStore_ThrowsOwnershipException()
    {
        // "S1" and "s1" name the same repository
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(GitHubContentOwnership.DescriptionPrefix + "S1"))));

        var error = await Assert.ThrowsAsync<GitRepositoryOwnershipException>(() =>
            Provisioner(github).EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken));

        Assert.Contains("belongs to store \"S1\"", error.Message);
        github.AssertDone();
    }

    [Fact]
    public async Task EnsureRepositoryAsync_MissingStoreSettingRepositoryInOrganization_Throws()
    {
        var github = new GitHubScript(
            ("GET", "repos/acme/website", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<GitContentException>(() =>
            Provisioner(github, repository: "acme/website").EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken));

        github.AssertDone();
    }

    [Fact]
    public async Task EnsureRepositoryAsync_ArchivedStoreSettingRepository_LeavesItArchived()
    {
        var github = new GitHubScript(
            ("GET", "repos/acme/website", (_, _) => GitHubScript.Json(RepoJson("Our web site", archived: true))),
            ("GET", "repos/acme/website/git/ref/heads/main", (_, _) => Branch()));

        await Provisioner(github, repository: "acme/website").EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task EnsureRepositoryAsync_MissingStoreSettingRepositoryOutsideOrganization_Throws()
    {
        var github = new GitHubScript(
            ("GET", "repos/other/website", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<GitContentException>(() =>
            Provisioner(github, repository: "other/website").EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken));

        github.AssertDone();
    }

    [Theory]
    [InlineData("acme", null)]
    [InlineData(null, "acme/website")]
    public async Task EnsureRepositoryAsync_AssignedRepositoryWithoutBaseBranch_ThrowsWithoutCuttingBranches(string organization, string shared)
    {
        // its deployment follows its own branches, not ones cut from its default branch
        var github = new GitHubScript(
            ("GET", "repos/acme/website", (_, _) => GitHubScript.Json(RepoJson("Our web site", defaultBranch: "master"))),
            ("GET", "repos/acme/website/git/ref/heads/main", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));
        var provisioner = Provisioner(github, repository: shared == null ? "acme/website" : null, configure: x =>
        {
            x.Organization = organization;
            x.Repository = shared;
            x.ReleaseBranch = "release";
        });

        var error = await Assert.ThrowsAsync<GitContentException>(() => provisioner.EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken));

        Assert.Contains("\"main\"", error.Message);
        github.AssertDone();
    }

    [Fact]
    public async Task EnsureRepositoryAsync_NoBaseOrReleaseBranch_CutsThemFromDefaultBranch()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(Mine, defaultBranch: "master"))),
            ("GET", "repos/acme/site-s1/git/ref/heads/master", (_, _) => Branch()),
            ("GET", "repos/acme/site-s1/git/ref/heads/master", (_, _) => Branch()),
            ("POST", "repos/acme/site-s1/git/refs", (_, body) => Created(body, "refs/heads/main")),
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()),
            ("POST", "repos/acme/site-s1/git/refs", (_, body) => Created(body, "refs/heads/release")));

        await Provisioner(github, configure: x => x.ReleaseBranch = "release").EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task ConnectAsync_StoreOff_EnsuresRepositoryAndSwitchesStoreOn()
    {
        var settings = new FakeSettingsManager(value: false);
        var stores = new FakeStoreService(new Store { Id = "s1", Settings = [] });
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(Mine))),
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()));

        await Provisioner(github, settings, stores: stores).ConnectAsync("s1", TestContext.Current.CancellationToken);

        var store = Assert.Single(stores.Saved);
        Assert.Equal("s1", store.Id);
        var saved = Assert.Single(store.Settings);
        Assert.Equal(ModuleConstants.Settings.StoreLevelSettings.GitContentEnabled.Name, saved.Name);
        Assert.Equal(true, saved.Value);
    }

    [Fact]
    public async Task ArchiveRepositoryAsync_RepositoryOfStore_ArchivesIt()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(Mine))),
            ("PATCH", $"repos/{Repo}", (_, body) =>
            {
                Assert.True(JObject.Parse(body)["archived"]?.Value<bool>());
                return GitHubScript.Json("{}");
            }));

        await Provisioner(github).ArchiveRepositoryAsync("s1", Repo, TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Theory]
    [InlineData("Imported by hand")]
    [InlineData("Page Builder content of store other")]
    public async Task ArchiveRepositoryAsync_RepositoryNotMadeForStore_LeavesItAlone(string description)
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(description))));

        await Provisioner(github).ArchiveRepositoryAsync("s1", Repo, TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task ArchiveRepositoryAsync_RepositoryOutsideOrganization_DoesNotCallGitHub()
    {
        var github = new GitHubScript();

        await Provisioner(github).ArchiveRepositoryAsync("s1", "other/website", TestContext.Current.CancellationToken);

        Assert.Equal(0, github.Requests);
    }

    [Fact]
    public async Task GetStatusAsync_NoConnection_DoesNotCallGitHub()
    {
        var github = new GitHubScript();

        var status = await Provisioner(github, configure: x => x.Enabled = false).GetStatusAsync("s1", TestContext.Current.CancellationToken);

        Assert.False(status.Configured);
        Assert.Equal(0, github.Requests);
    }

    [Fact]
    public async Task GetStatusAsync_ExistingRepository_ReturnsUrlAndLastCommit()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(Mine))),
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()));

        var status = await Provisioner(github, new FakeSettingsManager(value: true)).GetStatusAsync("s1", TestContext.Current.CancellationToken);

        Assert.True(status.Configured);
        Assert.True(status.Enabled);
        Assert.Equal(Repo, status.Repository);
        Assert.Equal("https://github.com/acme/site-s1", status.Url);
        Assert.Equal(Sha, status.LastCommitSha);
        Assert.Null(status.Error);
    }

    [Fact]
    public async Task GetStatusAsync_MissingRepository_ReportsMissingOrganization()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
            ("GET", "orgs/acme", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

        var status = await Provisioner(github).GetStatusAsync("s1", TestContext.Current.CancellationToken);

        Assert.Null(status.Url);
        Assert.Contains("organization \"acme\" was not found", status.Error);
    }

    [Fact]
    public async Task GetStatusAsync_RepositoryOfAnotherStore_ReportsOwner()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(GitHubContentOwnership.DescriptionPrefix + "S1"))),
            ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()));

        var status = await Provisioner(github, new FakeSettingsManager(value: true)).GetStatusAsync("s1", TestContext.Current.CancellationToken);

        Assert.Contains("belongs to store \"S1\"", status.Error);
    }

    [Fact]
    public async Task GetStatusAsync_GitHubTimesOut_ReportsTimeout()
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.")));

        var status = await Provisioner(github, new FakeSettingsManager(value: true)).GetStatusAsync("s1", TestContext.Current.CancellationToken);

        Assert.True(status.Configured);
        Assert.Equal(Repo, status.Repository);
        Assert.Contains("HttpClient.Timeout", status.Error);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private static GitHubContentProvisioner Provisioner(GitHubScript github, FakeSettingsManager settings = null, string repository = null,
        Action<GitContentOptions> configure = null, FakeGitContentOwnership ownership = null, FakeStoreService stores = null)
    {
        var options = new GitContentOptions
        {
            Enabled = true,
            Organization = "acme",
            Token = "t0ken",
            BaseBranch = "main",
        };
        configure?.Invoke(options);
        var wrapped = Options.Create(options);

        settings ??= new FakeSettingsManager(value: true, repository: repository);
        var gitRepository = new GitHubContentRepository(github, new MemoryCache(new MemoryCacheOptions()), wrapped);

        ownership ??= new FakeGitContentOwnership();
        var connection = GitHubConnections.CreateService(options);
        var policy = new GitContentPolicy(connection, wrapped, settings, stores ?? new FakeStoreService(), ownership);
        return new NoWaitProvisioner(new GitHubApiClient(github), connection, gitRepository, policy, ownership, wrapped);
    }

    private sealed class NoWaitProvisioner(IGitHubApiClient gitHubApiClient, IGitHubConnectionService gitHubConnectionService, IGitContentRepository gitContentRepository,
        IGitContentPolicy gitContentPolicy, IGitContentOwnership gitContentOwnership, IOptions<GitContentOptions> options)
        : GitHubContentProvisioner(gitHubApiClient, gitHubConnectionService, gitContentRepository, gitContentPolicy, gitContentOwnership, options,
            NullLogger<GitHubContentProvisioner>.Instance)
    {
        protected override Task DelayBeforeRetryAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static string RepoJson(string description, bool archived = false, string defaultBranch = "main") => new JObject
    {
        ["full_name"] = Repo,
        ["html_url"] = "https://github.com/acme/site-s1",
        ["description"] = description,
        ["archived"] = archived,
        ["default_branch"] = defaultBranch,
    }.ToString();

    private static System.Net.Http.HttpResponseMessage Branch() => GitHubScript.Json($$"""{ "object": { "sha": "{{Sha}}" } }""");

    private static System.Net.Http.HttpResponseMessage Created(string body, string expectedRef)
    {
        Assert.Equal(expectedRef, JObject.Parse(body)["ref"]?.Value<string>());
        return GitHubScript.Json("{}", HttpStatusCode.Created);
    }
}
