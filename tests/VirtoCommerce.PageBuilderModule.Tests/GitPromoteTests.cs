using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// Promotion to production: the page's state on the base branch, placed onto the release branch as
    /// its own commit through a pull request.
    /// <para>
    /// Most of these are about the branch the promotion is assembled on. It is deterministic by design —
    /// two editors promoting the same page have to meet on one pull request — and that is exactly what
    /// makes it possible to inherit a leftover from a promotion that already shipped.
    /// </para>
    /// </summary>
    public class GitPromoteTests
    {
        private const string Login = "john";
        private const string Page = "/about-us.page";
        private const string RepoPath = "pages/about-us.page";
        // Named the way the controller names it — the template carries no {user}, and the slug is
        // hashed, so spelling the branch out here would be a second implementation to keep in step.
        private static string PromoteBranch =>
            GitPageLocation.BranchFor("promote/{slug}", userName: null, GitPageLocation.ContentPath("pages", Page));

        private const string StaleHead = "5ea51e0000000000000000000000000000000000";
        private const string OnDev = """{ "settings": { "type": "settings", "name": "About, new copy" }, "content": [] }""";
        private const string OnProduction = """{ "settings": { "type": "settings", "name": "About" }, "content": [] }""";

        [Fact]
        public async Task Promote_CutsThePromoteBranchFromTheReleaseBranch()
        {
            var repository = Repository();

            var result = await Controller(repository, Publisher()).GitPromote("vccom", Page, "pages");

            Assert.IsType<OkObjectResult>(result);
            var created = Assert.Single(repository.CreatedBranches);
            Assert.Equal(PromoteBranch, created.Branch);
            Assert.Equal("production", created.FromRef);
        }

        [Fact]
        public async Task Promote_CommitsWhatTheBaseBranchHolds_NotWhatProductionHas()
        {
            var repository = Repository();

            await Controller(repository, Publisher()).GitPromote("vccom", Page, "pages");

            var commit = Assert.Single(repository.Commits);
            Assert.Equal(RepoPath, commit.Path);
            Assert.Equal(PromoteBranch, commit.Branch);
            Assert.Equal(OnDev, commit.Content);
        }

        [Fact]
        public async Task Promote_ALeftoverBranchWithNoOpenPullRequest_IsCutAgain()
        {
            // The previous promotion queued an auto-merge, so nothing here deleted its branch, and GitHub
            // only does where the repository is configured to. That branch was squashed into production,
            // so its head is not on production's history: committing onto it again would make this
            // promotion a three-way merge of two full rewrites of the same file — a conflict that every
            // retry reproduces, because the stale merge base never moves.
            var repository = Repository();
            repository.BranchHeads[PromoteBranch] = StaleHead;

            await Controller(repository, Publisher(openPullRequest: null)).GitPromote("vccom", Page, "pages");

            Assert.Equal([PromoteBranch], repository.DeletedBranches);
            Assert.Equal("production", Assert.Single(repository.CreatedBranches).FromRef);
            Assert.Equal(PromoteBranch, Assert.Single(repository.Commits).Branch);
        }

        [Fact]
        public async Task Promote_WhileAPullRequestIsStillOpen_KeepsTheBranchAndItsPullRequest()
        {
            // promotion is per page rather than per editor: a second editor promoting the same page adds
            // to the open pull request instead of opening a competing one
            var repository = Repository();
            repository.BranchHeads[PromoteBranch] = StaleHead;

            await Controller(repository, Publisher(openPullRequest: 42)).GitPromote("vccom", Page, "pages");

            Assert.Empty(repository.DeletedBranches);
            Assert.Empty(repository.CreatedBranches);
            Assert.Equal(PromoteBranch, Assert.Single(repository.Commits).Branch);
        }

        [Fact]
        public async Task Promote_MergesThePromoteBranchIntoTheReleaseBranch()
        {
            var repository = Repository();
            var publisher = Publisher();

            await Controller(repository, publisher).GitPromote("vccom", Page, "pages");

            Assert.Equal((PromoteBranch, "production"), Assert.Single(publisher.Merges));
        }

        [Fact]
        public async Task Promote_AMergedPromotion_DeletesItsBranch()
        {
            var repository = Repository();

            await Controller(repository, Publisher(GitPublishState.Merged)).GitPromote("vccom", Page, "pages");

            Assert.Equal([PromoteBranch], repository.DeletedBranches);
        }

        [Fact]
        public async Task Promote_APageProductionAlreadyHolds_ShipsNothing()
        {
            // an empty pull request would leave the editor waiting for a merge that means nothing
            var repository = Repository(onDev: OnProduction);

            var result = await Controller(repository, Publisher()).GitPromote("vccom", Page, "pages");

            Assert.Equal(nameof(GitPublishState.AlreadyPublished), Value(Assert.IsType<OkObjectResult>(result).Value, "state"));
            Assert.Empty(repository.Commits);
            Assert.Empty(repository.CreatedBranches);
            Assert.Empty(repository.DeletedBranches);
        }

        [Fact]
        public async Task Promote_APageThatIsNotOnTheBaseBranchYet_IsRefused()
        {
            // production follows what has already been through the dev environment
            var repository = Repository(onDev: null);

            var result = await Controller(repository, Publisher()).GitPromote("vccom", Page, "pages");

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(repository.Commits);
        }

        [Fact]
        public async Task Promote_WithoutAReleaseBranch_SaysSoRatherThanPromotingSomewhereElse()
        {
            var repository = Repository();

            var result = await Controller(repository, Publisher(), releaseBranch: null).GitPromote("vccom", Page, "pages");

            var error = Assert.IsType<BadRequestObjectResult>(result).Value.ToString();
            Assert.Contains(nameof(GitContentOptions.ReleaseBranch), error);
            Assert.Empty(repository.Commits);
        }

        [Fact]
        public async Task Promote_WithTheGitFlowOff_IsNotFound()
        {
            var repository = Repository();

            var result = await Controller(repository, Publisher(), gitFlow: false).GitPromote("vccom", Page, "pages");

            Assert.IsType<NotFoundResult>(result);
            Assert.Empty(repository.Commits);
        }

        [Fact]
        public async Task Promote_WithoutThePromotePermission_IsForbidden()
        {
            var repository = Repository();

            var result = await Controller(repository, Publisher(), allowed: false).GitPromote("vccom", Page, "pages");

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(repository.Commits);
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static RecordingRepository Repository(string onDev = OnDev)
        {
            var repository = new RecordingRepository();
            if (onDev != null)
            {
                repository.Files[(RepoPath, "master")] = onDev;
            }

            repository.Files[(RepoPath, "production")] = OnProduction;
            repository.BranchHeads["production"] = "9111111111111111111111111111111111111111";

            return repository;
        }

        private static RecordingPublisher Publisher(GitPublishState state = GitPublishState.Pending, int? openPullRequest = null) =>
            new(state, openPullRequest);

        private static string Value(object body, string property) =>
            body.GetType().GetProperty(property)!.GetValue(body) as string;

        private static PageBuilderController Controller(RecordingRepository repository, RecordingPublisher publisher,
            bool gitFlow = true, bool allowed = true, string releaseBranch = "production")
        {
            var options = Options.Create(new GitContentOptions
            {
                Enabled = true,
                Repository = "o/r",
                Token = "t0ken",
                BaseBranch = "master",
                ReleaseBranch = releaseBranch,
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
                gitContentPolicy: new FakePolicy(gitFlow),
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

        private sealed class FakePolicy(bool enabled) : IGitContentPolicy
        {
            public Task<bool> IsEnabledForStoreAsync(string storeId, CancellationToken cancellationToken = default) =>
                Task.FromResult(enabled);
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

        private sealed class RecordingPublisher(GitPublishState state, int? openPullRequest) : IGitContentPublisher
        {
            public List<(string Branch, string Into)> Merges { get; } = [];

            public Task<GitPublishResult> MergeBranchAsync(string branch, string title, CancellationToken cancellationToken = default) =>
                MergeBranchIntoAsync(branch, title, "master", cancellationToken);

            public Task<GitPublishResult> MergeBranchIntoAsync(string branch, string title, string baseBranch, CancellationToken cancellationToken = default)
            {
                Merges.Add((branch, baseBranch));
                return Task.FromResult(new GitPublishResult { State = state, PullRequestNumber = 7 });
            }

            public Task<int?> GetOpenPullRequestNumberAsync(string branch, CancellationToken cancellationToken = default) =>
                Task.FromResult(openPullRequest);
        }

        private sealed class RecordingRepository : IGitContentRepository
        {
            public const string NewCommitSha = "abcdef0123456789abcdef0123456789abcdef01";

            public Dictionary<(string Path, string Ref), string> Files { get; } = [];
            public Dictionary<string, string> BranchHeads { get; } = [];

            public List<(string Path, string Content, string Branch, string Message, GitCommitAuthor Author)> Commits { get; } = [];
            public List<(string Branch, string FromRef)> CreatedBranches { get; } = [];
            public List<string> DeletedBranches { get; } = [];

            public Task<string> ReadFileAsync(string path, string gitRef, CancellationToken cancellationToken = default) =>
                Task.FromResult(Files.GetValueOrDefault((path, gitRef)));

            public Task<string> GetBranchHeadShaAsync(string branch, CancellationToken cancellationToken = default) =>
                Task.FromResult(BranchHeads.GetValueOrDefault(branch));

            public Task CreateBranchAsync(string branch, string fromRef, CancellationToken cancellationToken = default)
            {
                CreatedBranches.Add((branch, fromRef));
                BranchHeads[branch] = BranchHeads.GetValueOrDefault(fromRef, NewCommitSha);
                return Task.CompletedTask;
            }

            public Task DeleteBranchAsync(string branch, string pagePath, CancellationToken cancellationToken = default)
            {
                DeletedBranches.Add(branch);
                BranchHeads.Remove(branch);
                return Task.CompletedTask;
            }

            public void InvalidateRead(string path, string gitRef) { }

            public Task<string> CommitFileAsync(string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                Commits.Add((path, content, branch, message, author));
                return Task.FromResult(NewCommitSha);
            }

            public Task<string> DeleteFileAsync(string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}
