using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.ContentModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Common;
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
        public async Task Unpublish_WhenMerged_RemovesTheLivePage()
        {
            var repository = Repository();
            var publisher = Publisher(GitPublishState.Merged);
            publisher.OnMerge = () => repository.Files.Remove((RepoPath, "master"));
            var live = new FakeBlobStorage();
            live.Files[Page] = Published;

            await Controller(repository, publisher, live: live).GitUnpublish("s1", Page, "pages");

            Assert.Equal([Page], live.Removed);
            Assert.Empty(live.Files);
            Assert.Single(live.Events.OfType<PageBuilderContentChangedEvent>());
            var indexed = Assert.Single(live.Events.OfType<ContentFileChangedEvent>());
            Assert.Equal(("pages", "s1"), (indexed.ContentType, indexed.StoreId));
            var entry = Assert.Single(indexed.ChangedEntries);
            Assert.Null(entry.NewEntry);
            Assert.Equal(Page, entry.OldEntry.RelativeUrl);
            Assert.Equal(EntryState.Deleted, entry.EntryState);
        }

        [Fact]
        public async Task Unpublish_RetriedAfterItsMerge_TakesTheLivePageDown()
        {
            // The live removal failed after the merge, so unpublishing again finishes it.
            var repository = Repository(withBranch: false);
            repository.Files.Remove((RepoPath, "master"));
            var live = new FakeBlobStorage();
            live.Files[Page] = Published;

            var result = await Controller(repository, Publisher(), live: live, history: Unpublished()).GitUnpublish("s1", Page, "pages");

            Assert.Equal(nameof(GitPublishState.AlreadyPublished), JObject.FromObject(Assert.IsType<OkObjectResult>(result).Value!)["state"]!.Value<string>());
            Assert.Equal([Page], live.Removed);
            Assert.Empty(repository.DeletedFiles);
        }

        [Fact]
        public async Task Unpublish_RetriedOnAStaleRead_LeavesALivePageTheBranchHasAgain()
        {
            var repository = Repository(withBranch: false);
            repository.Files.Remove((RepoPath, "master"));
            repository.Files[(RepoPath, BranchHead)] = Published;
            var live = new FakeBlobStorage();
            live.Files[Page] = Published;

            var result = await Controller(repository, Publisher(), live: live, history: Unpublished()).GitUnpublish("s1", Page, "pages");

            Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(Published, live.Files[Page]);
            Assert.Empty(live.Removed);
        }

        [Theory]
        [InlineData(GitRepositorySource.Site, true, true)]
        [InlineData(GitRepositorySource.Site, false, false)]
        [InlineData(GitRepositorySource.Shared, true, false)]
        public async Task PublishStatus_OfAPageUnpublishedOnGitHub_TellsTheLivePageIsBehind(GitRepositorySource source, bool hadThePage, bool liveBehind)
        {
            // An unpublish merged on GitHub writes nothing live.
            var repository = Repository(withBranch: false);
            repository.Files.Remove((RepoPath, "master"));
            var live = new FakeBlobStorage();
            live.Files[Page] = Published;

            var result = await Controller(repository, Publisher(), live: live, source: source, history: hadThePage ? Unpublished() : null)
                .GitPublishStatus("s1", Page, "pages");

            var status = PlatformJson.ParseBody(result);
            Assert.False(status.Value<bool>("published"));
            Assert.Equal(liveBehind, status.Value<bool>("liveBehind"));
        }

        [Fact]
        public async Task Unpublish_RetriedInARepositoryAssignedByHand_LeavesTheLivePageToItsDeployment()
        {
            var repository = Repository(withBranch: false);
            repository.Files.Remove((RepoPath, "master"));

            var result = await Controller(repository, Publisher(), source: GitRepositorySource.Shared, history: Unpublished()).GitUnpublish("s1", Page, "pages");

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Unpublish_WhenMergedInARepositoryAssignedByHand_LeavesTheLivePageToItsDeployment()
        {
            var repository = Repository();
            var publisher = Publisher(GitPublishState.Merged);
            publisher.OnMerge = () => repository.Files.Remove((RepoPath, "master"));
            var live = new FakeBlobStorage();
            live.Files[Page] = Published;

            await Controller(repository, publisher, live: live, source: GitRepositorySource.StoreSetting).GitUnpublish("s1", Page, "pages");

            Assert.Equal(Published, live.Files[Page]);
            Assert.Empty(live.Removed);
        }


        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static GitPageHistory Unpublished() => new()
        {
            Versions = [new GitPageVersion { Sha = BranchHead, Message = "publish /about-us.page", Published = true, Branches = ["master"] }],
        };

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

        private static FakeGitContentPublisher Publisher(GitPublishState state = GitPublishState.AwaitingMerge) =>
            new(state, new GitPendingPublish { Number = 7, AutoMerging = false });

        private static PageBuilderController Controller(RecordingRepository repository, FakeGitContentPublisher publisher,
            FakeBlobStorage live = null, GitRepositorySource source = GitRepositorySource.Site, GitPageHistory history = null)
        {
            var options = Options.Create(new GitContentOptions
            {
                Enabled = true,
                Repository = "o/r",
                Token = "t0ken",
                BaseBranch = "master",
            });

            // The live storage stays null without a merge, so an unpublish waiting for its merge must not reach for it.
            var connection = GitHubConnections.CreateService(options.Value);
            var gitHistory = new FakeGitContentHistory(history);
            var filePageDrafts = TestServices.FilePageDrafts(live, options, connection, new FakeGitContentPolicy(source: source), repository, gitHistory, new FakeGitContentProvisioner());
            var controller = new PageBuilderController(
                storeService: live?.StoreService,
                pathResolver: live,
                blobContentStorageProviderFactory: live,
                publishingService: live,
                eventPublisher: live,
                gitContentOptions: options,
                gitHubConnectionService: connection,
                gitContentPolicy: new FakeGitContentPolicy(source: source),
                gitContentRepository: repository,
                gitContentHistory: gitHistory,
                gitContentPublisher: publisher,
                gitLivePageService: new GitLivePageService(repository, gitHistory, connection, live, live, live, live),
                settingsManager: null,
                authorizationService: new FakeAuthorizationService(true),
                distributedLock: new InProcessDistributedLock(),
                requestValidator: RequestValidators.Content("s1", "vccom"),
                contentHistory: new NoContentHistory(),
                themeDraftService: null,
                groupedPageService: null,
                sharedComponentPlacementValidator: null,
                designerContentService: TestServices.Designer(new NoContentHistory(), filePageDrafts, null, null, live, null, null),
                contentPermissionService: TestServices.Permissions(new FakeAuthorizationService(true)),
                filePageDraftService: filePageDrafts,
            designerSaveValidator: RequestValidators.DesignerSave,
            designerDiscardValidator: RequestValidators.DesignerDiscard);

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, Login)], "test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            };

            return controller;
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

            public Task<string> ReadFileAsync(string repository, string path, string gitRef, CancellationToken cancellationToken = default) =>
                Task.FromResult(Files.GetValueOrDefault((path, gitRef)));

            public Task<string> GetBranchHeadShaAsync(string repository, string branch, CancellationToken cancellationToken = default) =>
                Task.FromResult(BranchHeads.GetValueOrDefault(branch));

            public Task CreateBranchAsync(string repository, string branch, string fromRef, CancellationToken cancellationToken = default)
            {
                CreatedBranches.Add((branch, fromRef));
                BranchHeads[branch] = BranchHeads.GetValueOrDefault(fromRef, NewCommitSha);
                Files[(RepoPath, branch)] = Files.GetValueOrDefault((RepoPath, fromRef));
                return Task.CompletedTask;
            }

            public Task SetBranchAsync(string repository, string branch, string sha, string pagePath, CancellationToken cancellationToken = default)
            {
                BranchHeads[branch] = sha;
                return Task.CompletedTask;
            }

            public Task DeleteBranchAsync(string repository, string branch, string pagePath, CancellationToken cancellationToken = default)
            {
                DeletedBranches.Add(branch);
                return Task.CompletedTask;
            }

            public void InvalidateRead(string repository, string path, string gitRef) { }

            public Task<string> CommitFileAsync(string repository, string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                Files[(path, branch)] = content;
                return Task.FromResult(NewCommitSha);
            }

            public Task<string> DeleteFileAsync(string repository, string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
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
