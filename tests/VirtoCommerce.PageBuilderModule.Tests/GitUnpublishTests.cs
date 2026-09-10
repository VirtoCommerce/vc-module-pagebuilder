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
    /// Unpublishing a page in git: a commit that deletes the file from the editor's work branch, merged
    /// into the production branch.
    /// <para>
    /// The retry is what most of these are about. Where the content repository does not allow auto-merge,
    /// the merge can be refused by a required check and then nothing will perform it later — the editor
    /// has to ask again, and this endpoint has to survive being asked, because by then the deleting
    /// commit is already on the branch.
    /// </para>
    /// </summary>
    public class GitUnpublishTests
    {
        private const string Login = "john";
        private const string Page = "/about-us.page";
        private const string RepoPath = "pages/about-us.page";
        private const string Published = """{ "settings": { "type": "settings", "name": "About" }, "content": [] }""";
        private const string BranchHead = "9111111111111111111111111111111111111111";

        private static string MyBranch => GitPageLocation.BranchFor("designer/{user}/{slug}", Login, "about-us.page");

        [Fact]
        public async Task Unpublish_CommitsTheDeletionOnMyBranchAndMergesIt()
        {
            var repository = Repository();
            var publisher = Publisher();

            var result = await Controller(repository, publisher).GitUnpublish("vccom", Page, "pages");

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal((RepoPath, MyBranch), Assert.Single(repository.DeletedFiles));
            Assert.Equal((MyBranch, "master"), Assert.Single(publisher.Merges));
        }

        [Fact]
        public async Task Unpublish_WithoutAWorkBranchYet_CutsOneFromTheProductionBranch()
        {
            var repository = Repository(withBranch: false);

            await Controller(repository, Publisher()).GitUnpublish("vccom", Page, "pages");

            Assert.Equal((MyBranch, "master"), Assert.Single(repository.CreatedBranches));
            Assert.Equal((RepoPath, MyBranch), Assert.Single(repository.DeletedFiles));
        }

        /// <summary>
        /// The editor pressing Unpublish again on a page whose earlier unpublish is awaiting a merge. The
        /// deleting commit is on the branch already; deleting a file that is not there is an error, so
        /// repeating it would fail the very retry that is meant to finish the operation.
        /// </summary>
        [Fact]
        public async Task Unpublish_Retried_MergesWithoutDeletingTwice()
        {
            var repository = Repository();
            repository.Files.Remove((RepoPath, MyBranch));
            var publisher = Publisher();

            var result = await Controller(repository, publisher).GitUnpublish("vccom", Page, "pages");

            Assert.IsType<OkObjectResult>(result);
            Assert.Empty(repository.DeletedFiles);
            Assert.Equal((MyBranch, "master"), Assert.Single(publisher.Merges));
        }

        [Fact]
        public async Task Unpublish_APageTheProductionBranchDoesNotHave_IsNotFound()
        {
            var repository = Repository();
            repository.Files.Remove((RepoPath, "master"));

            var result = await Controller(repository, Publisher()).GitUnpublish("vccom", Page, "pages");

            Assert.IsType<NotFoundObjectResult>(result);
            Assert.Empty(repository.DeletedFiles);
        }

        [Fact]
        public async Task Unpublish_WithoutThePublishPermission_IsForbidden()
        {
            var repository = Repository();

            var result = await Controller(repository, Publisher(), allowed: false).GitUnpublish("vccom", Page, "pages");

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(repository.DeletedFiles);
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static RecordingRepository Repository(bool withBranch = true)
        {
            var repository = new RecordingRepository();
            repository.Files[(RepoPath, "master")] = Published;
            repository.BranchHeads["master"] = BranchHead;

            if (withBranch)
            {
                repository.Files[(RepoPath, MyBranch)] = Published;
                repository.BranchHeads[MyBranch] = BranchHead;
            }

            return repository;
        }

        private static RecordingPublisher Publisher(GitPublishState state = GitPublishState.AwaitingMerge) =>
            new(state);

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
                return Task.FromResult(new GitPublishResult { State = state, PullRequestNumber = 7 });
            }

            public Task<GitPendingPublish> GetOpenPullRequestAsync(string branch, CancellationToken cancellationToken = default) =>
                Task.FromResult(new GitPendingPublish { Number = 7, AutoMerging = false });
        }

        /// <summary>
        /// Refuses to delete a file it does not hold, the way the real repository does — that guard is
        /// what makes the retry test mean anything.
        /// </summary>
        private sealed class RecordingRepository : IGitContentRepository
        {
            public const string NewCommitSha = "abcdef0123456789abcdef0123456789abcdef01";

            public Dictionary<(string Path, string Ref), string> Files { get; } = [];
            public Dictionary<string, string> BranchHeads { get; } = [];

            public List<(string Branch, string FromRef)> CreatedBranches { get; } = [];
            public List<string> DeletedBranches { get; } = [];
            public List<(string Path, string Branch)> DeletedFiles { get; } = [];

            public Task<string> ReadFileAsync(string path, string gitRef, CancellationToken cancellationToken = default) =>
                Task.FromResult(Files.GetValueOrDefault((path, gitRef)));

            public Task<string> GetBranchHeadShaAsync(string branch, CancellationToken cancellationToken = default) =>
                Task.FromResult(BranchHeads.GetValueOrDefault(branch));

            public Task CreateBranchAsync(string branch, string fromRef, CancellationToken cancellationToken = default)
            {
                CreatedBranches.Add((branch, fromRef));
                BranchHeads[branch] = BranchHeads.GetValueOrDefault(fromRef, NewCommitSha);
                Files[(RepoPath, branch)] = Files.GetValueOrDefault((RepoPath, fromRef));
                return Task.CompletedTask;
            }

            public Task DeleteBranchAsync(string branch, string pagePath, CancellationToken cancellationToken = default)
            {
                DeletedBranches.Add(branch);
                return Task.CompletedTask;
            }

            public void InvalidateRead(string path, string gitRef) { }

            public Task<string> CommitFileAsync(string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                Files[(path, branch)] = content;
                return Task.FromResult(NewCommitSha);
            }

            public Task<string> DeleteFileAsync(string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                if (!Files.Remove((path, branch)))
                {
                    throw new InvalidOperationException($"Cannot delete \"{path}\": it does not exist on branch \"{branch}\".");
                }

                DeletedFiles.Add((path, branch));
                return Task.FromResult(NewCommitSha);
            }
        }
    }
}
