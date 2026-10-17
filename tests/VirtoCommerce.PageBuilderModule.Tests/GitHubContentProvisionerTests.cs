using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// A site's repository, from GitHub's side: created in the organization when the site needs one,
    /// adopted when it is already there, brought back from the archive with its store — and archived only
    /// when the module made it for that store.
    /// </summary>
    public class GitHubContentProvisionerTests
    {
        private const string Repo = "acme/site-s1";
        private const string Sha = "1111111111111111111111111111111111111111";
        private static readonly string Mine = GitHubContentProvisioner.Marker + "s1";

        [Fact]
        public async Task A_missing_repository_is_created_private_with_a_first_commit_and_marked_as_the_stores()
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

        /// <summary>The same store's first save and its "turn on" racing: GitHub says it exists, and that is the one wanted.</summary>
        [Fact]
        public async Task A_repository_created_meanwhile_is_adopted()
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
        public async Task A_store_that_comes_back_gets_its_archived_repository_back()
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

        /// <summary>Two store ids that sanitize alike would share a repository and publish over each other.</summary>
        [Fact]
        public async Task A_repository_made_for_another_store_is_refused()
        {
            var github = new GitHubScript(
                ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(GitHubContentProvisioner.Marker + "S1"))));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Provisioner(github).EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken));

            Assert.Contains("belongs to store \"S1\"", error.Message);
            github.AssertDone();
        }

        [Fact]
        public async Task A_missing_repository_the_store_named_itself_is_never_created_elsewhere()
        {
            var github = new GitHubScript(
                ("GET", "repos/other/website", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Provisioner(github, repository: "other/website").EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken));

            github.AssertDone();
        }

        [Fact]
        public async Task The_base_and_release_branches_are_cut_from_the_repositorys_own_default_branch()
        {
            var github = new GitHubScript(
                ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(Mine, defaultBranch: "master"))),
                ("GET", "repos/acme/site-s1/git/ref/heads/master", (_, _) => Branch()),
                // main from master
                ("GET", "repos/acme/site-s1/git/ref/heads/master", (_, _) => Branch()),
                ("POST", "repos/acme/site-s1/git/refs", (_, body) => Created(body, "refs/heads/main")),
                // release from main
                ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()),
                ("POST", "repos/acme/site-s1/git/refs", (_, body) => Created(body, "refs/heads/release")));

            await Provisioner(github, configure: x => x.ReleaseBranch = "release").EnsureRepositoryAsync("s1", TestContext.Current.CancellationToken);

            github.AssertDone();
        }

        [Fact]
        public async Task Connect_makes_the_repository_ready_then_switches_the_store_on()
        {
            var settings = new FakeSettingsManager(value: false);
            var github = new GitHubScript(
                ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(Mine))),
                ("GET", "repos/acme/site-s1/git/ref/heads/main", (_, _) => Branch()));

            await Provisioner(github, settings).ConnectAsync("s1", TestContext.Current.CancellationToken);

            var saved = Assert.Single(settings.Saved);
            Assert.Equal(ModuleConstants.Settings.StoreLevelSettings.GitContentEnabled.Name, saved.Name);
            Assert.Equal("s1", saved.ObjectId);
            Assert.Equal(true, saved.Value);
        }

        [Fact]
        public async Task A_deleted_stores_repository_is_archived()
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
        public async Task A_repository_the_module_did_not_make_for_that_store_is_never_archived(string description)
        {
            var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json(RepoJson(description))));

            await Provisioner(github).ArchiveRepositoryAsync("s1", Repo, TestContext.Current.CancellationToken);

            github.AssertDone();
        }

        [Fact]
        public async Task Archiving_outside_the_organization_asks_GitHub_nothing()
        {
            var github = new GitHubScript();

            await Provisioner(github).ArchiveRepositoryAsync("s1", "other/website", TestContext.Current.CancellationToken);

            Assert.Equal(0, github.Requests);
        }

        [Fact]
        public async Task Status_without_a_connection_asks_GitHub_nothing()
        {
            var github = new GitHubScript();

            var status = await Provisioner(github, configure: x => x.Enabled = false).GetStatusAsync("s1", TestContext.Current.CancellationToken);

            Assert.False(status.Configured);
            Assert.Equal(0, github.Requests);
        }

        [Fact]
        public async Task Status_of_a_live_repository_links_it_and_its_last_change()
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

        /// <summary>Not created yet is fine — as long as there is an organization to create it in.</summary>
        [Fact]
        public async Task Status_of_a_repository_not_made_yet_checks_the_organization_is_there()
        {
            var github = new GitHubScript(
                ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
                ("GET", "orgs/acme", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

            var status = await Provisioner(github).GetStatusAsync("s1", TestContext.Current.CancellationToken);

            Assert.Null(status.Url);
            Assert.Contains("organization \"acme\" was not found", status.Error);
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static GitHubContentProvisioner Provisioner(GitHubScript github, FakeSettingsManager settings = null, string repository = null,
            Action<GitContentOptions> configure = null)
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

            return new GitHubContentProvisioner(github, gitRepository, new GitContentPolicy(wrapped, settings), settings, wrapped,
                NullLogger<GitHubContentProvisioner>.Instance)
            {
                BranchWait = TimeSpan.Zero,
            };
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
}
