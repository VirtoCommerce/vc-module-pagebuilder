using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// Publishing a page in git, and the one state that used to have no way out of the builder: the
    /// draft conflicts with what the base branch holds now.
    /// <para>
    /// A .page is one JSON document, so two edits of the same page through two routes can rarely be
    /// merged by git, and the work branch keeps its old merge base — every Publish reused the same pull
    /// request and met the same refusal. <c>rebase=true</c> is the editor's answer to that: the branch is
    /// cut again from the base branch and the draft's bytes become one commit on it, the way promotion
    /// moves a file's state rather than its history.
    /// </para>
    /// </summary>
    public class GitPublishTests
    {
        private const string Login = "john";
        private const string Page = "/about-us.page";
        private const string RepoPath = "pages/about-us.page";
        private const string Published = """{ "settings": { "type": "settings", "name": "About" }, "content": [] }""";
        private const string Draft = """{ "settings": { "type": "settings", "name": "About, rewritten" }, "content": [] }""";
        private const string BranchHead = "9111111111111111111111111111111111111111";

        private static string MyBranch => GitPageLocation.BranchFor("designer/{user}/{slug}", Login, "about-us.page");

        [Fact]
        public async Task Publish_MergesTheEditorsBranchIntoTheBaseBranch()
        {
            var repository = Repository();
            var publisher = Publisher(GitPublishState.Merged);

            var result = await Controller(repository, publisher).GitPublish("vccom", Page, "pages");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Merged", JObject.FromObject(ok.Value)["state"]?.Value<string>());
            Assert.Equal((MyBranch, "master"), Assert.Single(publisher.Merges));
            Assert.Empty(repository.CreatedBranches);
            Assert.Empty(repository.Commits);
        }

        [Fact]
        public async Task Publish_WhenTheMergeConflicts_Is409_AndOffersARebase()
        {
            var repository = Repository();

            var result = await Controller(repository, Publisher(GitPublishState.Conflict)).GitPublish("vccom", Page, "pages");

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var body = JObject.FromObject(conflict.Value);
            Assert.True(body["canRebase"]?.Value<bool>());
            Assert.Contains("master", body["error"]?.Value<string>());
            Assert.Equal(7, body["pullRequest"]?.Value<int>());
            // nothing was rewritten behind the editor's back
            Assert.Empty(repository.DeletedBranches);
            Assert.Empty(repository.Commits);
        }

        /// <summary>
        /// The way out. The old branch goes (and its pull request with it), a fresh one is cut from the
        /// base branch, and the draft — exactly its bytes, not a merge of anything — is the one commit on
        /// it. Only then is the merge asked for.
        /// </summary>
        [Fact]
        public async Task Publish_WithRebase_CutsTheBranchAgainFromTheBaseBranch_AndCommitsTheDraftAsItIs()
        {
            var repository = Repository();
            var publisher = Publisher(GitPublishState.Merged);

            var result = await Controller(repository, publisher).GitPublish("vccom", Page, "pages", rebase: true);

            Assert.IsType<OkObjectResult>(result);
            Assert.Contains(MyBranch, repository.DeletedBranches);
            Assert.Equal((MyBranch, "master"), Assert.Single(repository.CreatedBranches));

            var commit = Assert.Single(repository.Commits);
            Assert.Equal(RepoPath, commit.Path);
            Assert.Equal(MyBranch, commit.Branch);
            Assert.Equal(Draft, commit.Content);
            Assert.Contains("replacing what changed", commit.Message);

            Assert.Equal((MyBranch, "master"), Assert.Single(publisher.Merges));
            Assert.True(repository.Order.IndexOf("delete") < repository.Order.IndexOf("create"), "the branch is deleted before it is cut again");
            Assert.True(repository.Order.IndexOf("create") < repository.Order.IndexOf("commit"), "the draft is committed onto the fresh branch");
        }

        [Fact]
        public async Task Publish_WithRebase_ButNoDraftOfMine_IsAlreadyPublished_AndTouchesNoBranch()
        {
            var repository = Repository(withDraft: false);
            var publisher = Publisher(GitPublishState.Merged);

            var result = await Controller(repository, publisher).GitPublish("vccom", Page, "pages", rebase: true);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("AlreadyPublished", JObject.FromObject(ok.Value)["state"]?.Value<string>());
            Assert.Empty(repository.DeletedBranches);
            Assert.Empty(repository.CreatedBranches);
            Assert.Empty(publisher.Merges);
        }

        [Fact]
        public async Task Publish_WithoutThePublishPermission_IsForbidden_RebaseOrNot()
        {
            var repository = Repository();

            var result = await Controller(repository, Publisher(GitPublishState.Merged), allowed: false).GitPublish("vccom", Page, "pages", rebase: true);

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(repository.DeletedBranches);
            Assert.Empty(repository.Commits);
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static RecordingRepository Repository(bool withDraft = true)
        {
            var repository = new RecordingRepository();
            repository.Files[(RepoPath, "master")] = Published;
            repository.BranchHeads["master"] = BranchHead;

            if (withDraft)
            {
                repository.Files[(RepoPath, MyBranch)] = Draft;
                repository.BranchHeads[MyBranch] = BranchHead;
            }

            return repository;
        }

        private static RecordingPublisher Publisher(GitPublishState state) => new(state);

        private static PageBuilderController Controller(RecordingRepository repository, RecordingPublisher publisher,
            bool allowed = true)
        {
            var options = Options.Create(new GitContentOptions
            {
                Enabled = true,
                Repository = "o/r",
                Token = "t0ken",
                BaseBranch = "master",
            });

            // Only the git dependencies take part in this path; the rest are left null on purpose, so a
            // change that starts reaching for blob storage fails loudly here.
            var controller = new PageBuilderController(
                storeService: null,
                pathResolver: null,
                blobContentStorageProviderFactory: null,
                publishingService: null,
                eventPublisher: null,
                gitContentOptions: options,
                gitContentPolicy: new FakePolicy(),
                gitContentRepository: repository,
                gitContentHistory: new SilentHistory(),
                gitContentPublisher: publisher,
                settingsManager: null,
                authorizationService: new FakeAuthorization(allowed));

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, Login)], "test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            };

            return controller;
        }

        private sealed class FakePolicy : IGitContentPolicy
        {
            public Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default) =>
                Task.FromResult(true);
        }

        private sealed class FakeAuthorization(bool allowed) : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
                Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        private sealed class SilentHistory : IGitContentHistory
        {
            public Task<GitPageHistory> GetHistoryAsync(string repoPath, GitHistoryQuery query, CancellationToken cancellationToken = default) =>
                Task.FromResult(new GitPageHistory());

            public void Invalidate(string repoPath) { }
        }

        private sealed class RecordingPublisher(GitPublishState state) : IGitContentPublisher
        {
            public List<(string Branch, string Into)> Merges { get; } = [];

            public Task<GitPublishResult> MergeBranchAsync(string branch, string title, CancellationToken cancellationToken = default) =>
                MergeBranchIntoAsync(branch, title, "master", cancellationToken);

            public Task<GitPublishResult> MergeBranchIntoAsync(string branch, string title, string baseBranch, CancellationToken cancellationToken = default)
            {
                Merges.Add((branch, baseBranch));
                return Task.FromResult(new GitPublishResult { State = state, PullRequestNumber = 7, Url = "https://github.com/o/r/pull/7" });
            }

            public Task<GitPendingPublish> GetOpenPullRequestAsync(string branch, CancellationToken cancellationToken = default) =>
                Task.FromResult<GitPendingPublish>(null);
        }

        /// <summary>
        /// Behaves like the real repository where it matters here: a branch cut from a ref starts with that
        /// ref's copy of the page, and deleting a branch takes its copy away.
        /// </summary>
        private sealed class RecordingRepository : IGitContentRepository
        {
            public const string NewCommitSha = "abcdef0123456789abcdef0123456789abcdef01";

            public Dictionary<(string Path, string Ref), string> Files { get; } = [];
            public Dictionary<string, string> BranchHeads { get; } = [];

            public List<(string Branch, string FromRef)> CreatedBranches { get; } = [];
            public List<string> DeletedBranches { get; } = [];
            public List<(string Path, string Branch, string Content, string Message)> Commits { get; } = [];
            public List<string> Order { get; } = [];

            public Task<string> ReadFileAsync(string path, string gitRef, CancellationToken cancellationToken = default) =>
                Task.FromResult(Files.GetValueOrDefault((path, gitRef)));

            public Task<string> GetBranchHeadShaAsync(string branch, CancellationToken cancellationToken = default) =>
                Task.FromResult(BranchHeads.GetValueOrDefault(branch));

            public Task CreateBranchAsync(string branch, string fromRef, CancellationToken cancellationToken = default)
            {
                Order.Add("create");
                CreatedBranches.Add((branch, fromRef));
                BranchHeads[branch] = BranchHeads.GetValueOrDefault(fromRef, NewCommitSha);
                Files[(RepoPath, branch)] = Files.GetValueOrDefault((RepoPath, fromRef));
                return Task.CompletedTask;
            }

            public Task DeleteBranchAsync(string branch, string pagePath, CancellationToken cancellationToken = default)
            {
                Order.Add("delete");
                DeletedBranches.Add(branch);
                BranchHeads.Remove(branch);
                Files.Remove((pagePath, branch));
                return Task.CompletedTask;
            }

            public void InvalidateRead(string path, string gitRef) { }

            public Task<string> CommitFileAsync(string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                if (!BranchHeads.ContainsKey(branch))
                {
                    throw new InvalidOperationException($"Cannot commit to \"{branch}\": the branch does not exist.");
                }

                Order.Add("commit");
                Commits.Add((path, branch, content, message));
                Files[(path, branch)] = content;
                return Task.FromResult(NewCommitSha);
            }

            public Task<string> DeleteFileAsync(string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                Files.Remove((path, branch));
                return Task.FromResult(NewCommitSha);
            }
        }
    }
}
