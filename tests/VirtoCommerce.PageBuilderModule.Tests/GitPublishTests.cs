using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private const string BaseHead = "9111111111111111111111111111111111111111";
        private const string DraftHead = "7222222222222222222222222222222222222222";

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
            Assert.Empty(repository.MovedBranches);
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
        /// The way out. The branch is moved onto the base branch — not deleted and cut again, which would
        /// leave a moment with no branch at all — and the draft, exactly its bytes and not a merge of
        /// anything, becomes the one commit on it. Only then is the merge asked for.
        /// </summary>
        [Fact]
        public async Task Publish_WithRebase_MovesTheBranchOntoTheBaseBranch_AndCommitsTheDraftAsItIs()
        {
            var repository = Repository();
            var publisher = Publisher(GitPublishState.Merged);

            var result = await Controller(repository, publisher).GitPublish("vccom", Page, "pages", rebase: true);

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal((MyBranch, BaseHead), Assert.Single(repository.MovedBranches));
            Assert.Empty(repository.CreatedBranches);

            var commit = Assert.Single(repository.Commits);
            Assert.Equal(RepoPath, commit.Path);
            Assert.Equal(MyBranch, commit.Branch);
            Assert.Equal(Draft, commit.Content);
            Assert.Contains("replacing what changed", commit.Message);

            Assert.Equal((MyBranch, "master"), Assert.Single(publisher.Merges));
            Assert.True(repository.Order.IndexOf("move") < repository.Order.IndexOf("commit"), "the draft is committed after the branch has moved");
            // The branch is deleted here too, but only at the end: that is the published branch being
            // cleaned up after the merge, not the rebuild taking it away before the draft is safe.
            Assert.True(repository.Order.IndexOf("delete") > repository.Order.IndexOf("commit"),
                "nothing deletes the branch while the draft is only on it");
        }

        /// <summary>
        /// The window that makes the order matter: between moving the branch and committing the draft onto
        /// it, the draft is a commit nothing points at. If the commit fails there, the branch goes back
        /// where it was — otherwise the next publish would read no draft and answer AlreadyPublished, which
        /// tells an editor their page is live while their version is gone.
        /// </summary>
        [Fact]
        public async Task Publish_WithRebase_WhenTheCommitFails_PutsTheDraftBranchBack()
        {
            var repository = Repository();
            repository.FailNextCommit = true;

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                Controller(repository, Publisher(GitPublishState.Merged)).GitPublish("vccom", Page, "pages", rebase: true));

            Assert.Equal([(MyBranch, BaseHead), (MyBranch, DraftHead)], repository.MovedBranches);
            Assert.Equal(DraftHead, repository.BranchHeads[MyBranch]);
            Assert.Equal(Draft, repository.Files[(RepoPath, MyBranch)]);
        }

        /// <summary>
        /// And when even putting it back fails, the sha is the only way left to find the draft — so it is
        /// in the message rather than in a log nobody reads.
        /// </summary>
        [Fact]
        public async Task Publish_WithRebase_WhenTheUndoFailsToo_NamesTheCommitTheDraftIsAt()
        {
            var repository = Repository();
            repository.FailNextCommit = true;
            repository.FailRestore = true;

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Controller(repository, Publisher(GitPublishState.Merged)).GitPublish("vccom", Page, "pages", rebase: true));

            Assert.Contains(DraftHead, failure.Message);
            Assert.Contains($"git branch {MyBranch} {DraftHead}", failure.Message);
            Assert.IsType<AggregateException>(failure.InnerException);
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
            repository.BranchHeads["master"] = BaseHead;

            if (withDraft)
            {
                repository.Files[(RepoPath, MyBranch)] = Draft;
                repository.BranchHeads[MyBranch] = DraftHead;
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
            public List<(string Branch, string Sha)> MovedBranches { get; } = [];
            public List<string> DeletedBranches { get; } = [];
            public List<(string Path, string Branch, string Content, string Message)> Commits { get; } = [];
            public List<string> Order { get; } = [];

            /// <summary>The rebuild fails where it hurts: after the branch has moved, before the draft is on it.</summary>
            public bool FailNextCommit { get; set; }

            /// <summary>And the undo fails too — the draft is then only findable by its sha.</summary>
            public bool FailRestore { get; set; }

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

            /// <summary>
            /// Moves the ref, and with it what reading the branch answers — the real repository is a
            /// pointer to a commit, and the file on the branch is whatever that commit holds. Here that is
            /// the base branch's copy on the way out, and the draft again on the way back.
            /// </summary>
            public Task SetBranchAsync(string branch, string sha, string pagePath, CancellationToken cancellationToken = default)
            {
                if (FailRestore && MovedBranches.Count > 0)
                {
                    throw new HttpRequestException($"could not move \"{branch}\"");
                }

                Order.Add("move");
                MovedBranches.Add((branch, sha));
                BranchHeads[branch] = sha;

                var at = BranchHeads.FirstOrDefault(head => head.Value == sha && head.Key != branch).Key;
                Files[(pagePath, branch)] = at != null ? Files.GetValueOrDefault((pagePath, at)) : Draft;
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
                if (FailNextCommit)
                {
                    FailNextCommit = false;
                    throw new HttpRequestException($"could not commit \"{path}\" to branch \"{branch}\"");
                }

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
