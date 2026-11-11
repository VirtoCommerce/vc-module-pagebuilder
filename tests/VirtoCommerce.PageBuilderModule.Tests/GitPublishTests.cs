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
using VirtoCommerce.ContentModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Theme;
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
        private const string LivePage = "pages/s1/about-us.page";
        private const string RepoPath = "pages/about-us.page";
        private const string Published = """{ "settings": { "type": "settings", "name": "About" }, "content": [] }""";
        private const string Draft = """{ "settings": { "type": "settings", "name": "About, rewritten" }, "content": [] }""";
        private const string LastWeek = """{ "settings": { "type": "settings", "name": "About, last week" }, "content": [] }""";
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
        /// The cancellation the undo exists for. An editor closing the tab while the commit is in flight
        /// aborts the request, and the rebuild fails exactly where the draft is only reachable by sha. If
        /// the undo were given the request's token — the one that has just been cancelled — it could not
        /// run at all, and the next publish would read no draft and answer AlreadyPublished.
        /// </summary>
        [Fact]
        public async Task Publish_WithRebase_WhenTheRequestIsAborted_StillPutsTheDraftBranchBack()
        {
            var repository = Repository();
            var aborted = new CancellationTokenSource();
            repository.CancelOnCommit = aborted;

            var controller = Controller(repository, Publisher(GitPublishState.Merged));
            controller.ControllerContext.HttpContext.RequestAborted = aborted.Token;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                controller.GitPublish("vccom", Page, "pages", rebase: true));

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
        public async Task Publish_WhenTheRepositoryCannotBeRead_FailsInsteadOfAnsweringAlreadyPublished()
        {
            // GitHub answers a missing repository, or one the platform cannot see, the way it answers a missing file.
            var live = new FakeBlobStorage();
            live.Files[LivePage] = Published;

            var failure = await Assert.ThrowsAsync<GitContentException>(() =>
                Controller(new RecordingRepository(), Publisher(GitPublishState.Merged), live: live).GitPublish("s1", Page, "pages"));

            Assert.Equal("The GitHub repository \"acme/site-s1\" does not exist, or the platform has no access to it.", failure.Message);
            Assert.Equal(Published, live.Files[LivePage]);
            Assert.Empty(live.Events);
        }

        [Fact]
        public async Task Publish_WhenMerged_WritesTheLivePageAsTheBaseBranchHasIt_NotTheDraft()
        {
            var repository = Repository();
            var live = new FakeBlobStorage();

            await Controller(repository, Publisher(GitPublishState.Merged), live: live).GitPublish("s1", Page, "pages");

            Assert.True(JToken.DeepEquals(JToken.Parse(Published), JToken.Parse(live.Files[LivePage])));
            Assert.Single(live.Events.OfType<PageBuilderContentChangedEvent>());
            var indexed = Assert.Single(live.Events.OfType<ContentFileChangedEvent>());
            Assert.Equal(("pages", "s1"), (indexed.ContentType, indexed.StoreId));
            var entry = Assert.Single(indexed.ChangedEntries);
            Assert.Equal(Page, entry.NewEntry.RelativeUrl);
            Assert.Equal(["acme/site-s1"], repository.Repositories);
        }

        [Theory]
        [InlineData(GitPublishState.Pending)]
        [InlineData(GitPublishState.AwaitingMerge)]
        [InlineData(GitPublishState.Conflict)]
        public async Task Publish_WhenNotMerged_LeavesTheLivePageAlone(GitPublishState state)
        {
            var live = new FakeBlobStorage();

            await Controller(Repository(), Publisher(state), live: live).GitPublish("s1", Page, "pages");

            Assert.Empty(live.Files);
            Assert.Empty(live.Events);
        }

        [Theory]
        [InlineData(GitRepositorySource.Shared)]
        [InlineData(GitRepositorySource.StoreSetting)]
        public async Task Publish_InARepositoryAssignedByHand_LeavesTheLivePageToItsDeployment(GitRepositorySource source)
        {
            var repository = Repository();
            var publisher = Publisher(GitPublishState.Merged);
            var live = new FakeBlobStorage();

            await Controller(repository, publisher, live: live, source: source).GitPublish("s1", Page, "pages");
            await Controller(Repository(withDraft: false), publisher, live: live, source: source).GitPublish("s1", Page, "pages");

            Assert.Empty(live.Files);
            Assert.Empty(live.Events);
        }

        [Fact]
        public async Task Publish_WithNothingOfMine_StillWritesTheLivePageFromTheBaseBranch()
        {
            // Publishing again repairs a live write that failed after its merge.
            var live = new FakeBlobStorage();

            await Controller(Repository(withDraft: false), Publisher(GitPublishState.Merged), live: live).GitPublish("s1", Page, "pages");

            Assert.True(JToken.DeepEquals(JToken.Parse(Published), JToken.Parse(live.Files[LivePage])));
        }

        [Fact]
        public async Task Publish_BaseBranchPageNotJson_ThrowsGitContentExceptionAndLeavesTheLivePageAlone()
        {
            var repository = Repository(withDraft: false);
            repository.Files[(RepoPath, "master")] = "{ \"settings\": ";
            var live = new FakeBlobStorage();
            live.Files[LivePage] = LastWeek;

            await Assert.ThrowsAsync<GitContentException>(() =>
                Controller(repository, Publisher(GitPublishState.Merged), live: live).GitPublish("s1", Page, "pages"));

            Assert.Equal(LastWeek, live.Files[LivePage]);
        }

        [Fact]
        public async Task Publish_OfADraftTheBaseBranchAlreadyHas_WritesTheLivePageFromTheBaseBranch()
        {
            // A pull request merged on GitHub writes nothing live.
            var live = new FakeBlobStorage();

            await Controller(MergedOnGitHub(), Publisher(GitPublishState.AlreadyPublished), live: live).GitPublish("s1", Page, "pages");

            Assert.True(JToken.DeepEquals(JToken.Parse(Published), JToken.Parse(live.Files[LivePage])));
        }

        [Theory]
        [InlineData(GitRepositorySource.Shared)]
        [InlineData(GitRepositorySource.StoreSetting)]
        public async Task Publish_OfADraftTheBaseBranchAlreadyHas_InARepositoryAssignedByHand_LeavesTheLivePageAlone(GitRepositorySource source)
        {
            var live = new FakeBlobStorage();

            await Controller(MergedOnGitHub(), Publisher(GitPublishState.AlreadyPublished), live: live, source: source).GitPublish("s1", Page, "pages");

            Assert.Empty(live.Files);
        }

        [Fact]
        public async Task GitPublishStatus_ReleaseBranchAndLivePageBehind_WritesTheStatusJson()
        {
            var live = new FakeBlobStorage();
            live.Files[LivePage] = LastWeek;

            var result = await Controller(Repository(), Publisher(GitPublishState.Merged), live: live, releaseBranch: "production").GitPublishStatus("s1", Page, "pages");

            Assert.Equal(
                """{"published":true,"hasChanges":true,"pending":false,"awaitingMerge":false,"flow":"git","production":{"published":false,"behind":true,"pending":false,"awaitingMerge":false},"liveBehind":true}""",
                PlatformJson.SerializeBody(result));
        }

        [Theory]
        [InlineData(GitRepositorySource.Site, LastWeek, true)]
        [InlineData(GitRepositorySource.Site, null, true)]
        [InlineData(GitRepositorySource.Site, """{"settings":{"name":"About","type":"settings"},"content":[]}""", false)]
        [InlineData(GitRepositorySource.Shared, LastWeek, false)]
        public async Task PublishStatus_TellsALivePageBehindTheBaseBranch_WhereThePlatformWritesLive(GitRepositorySource source, string livePage, bool liveBehind)
        {
            var live = new FakeBlobStorage();
            if (livePage != null)
            {
                live.Files[LivePage] = livePage;
            }

            var result = await Controller(MergedOnGitHub(), Publisher(GitPublishState.AlreadyPublished), live: live, source: source).GitPublishStatus("s1", Page, "pages");

            var status = PlatformJson.ParseBody(result);
            Assert.True(status.Value<bool>("published"));
            Assert.False(status.Value<bool>("hasChanges"));
            Assert.Equal(liveBehind, status.Value<bool>("liveBehind"));
        }

        [Fact]
        public async Task Publish_OfAPageTheBranchDoesNotHave_NeverRemovesItsLiveCopy()
        {
            var repository = Repository(withDraft: false);
            repository.Files.Remove((RepoPath, "master"));
            var live = new FakeBlobStorage();
            live.Files[LivePage] = Published;

            await Controller(repository, Publisher(GitPublishState.Merged), live: live).GitPublish("s1", Page, "pages");

            Assert.Equal(Published, live.Files[LivePage]);
            Assert.Empty(live.Removed);
            Assert.Empty(live.Events);
        }

        [Fact]
        public async Task Save_WhenTheRepositoryHasNoBaseBranch_HasItMadeFirst()
        {
            var repository = new RecordingRepository();
            var provisioner = new FakeGitContentProvisioner { OnEnsure = () => repository.BranchHeads["master"] = BaseHead };
            var files = new JArray { new JObject { ["path"] = Page, ["type"] = "pages", ["content"] = JObject.Parse(Draft) } };

            await Controller(repository, Publisher(GitPublishState.Merged), provisioner: provisioner)
                .SaveTemplates("s1", null, new PageBuilderController.SaveFilesModel { Files = files.ToString() }, draft: true);

            Assert.Equal(["s1"], provisioner.Ensured);
            Assert.Equal((MyBranch, "master"), Assert.Single(repository.CreatedBranches));
            Assert.Single(repository.Commits);
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

        private static RecordingRepository MergedOnGitHub()
        {
            var repository = Repository(withDraft: false);
            repository.Files[(RepoPath, MyBranch)] = Published;
            repository.BranchHeads[MyBranch] = BaseHead;
            return repository;
        }

        private static FakeGitContentPublisher Publisher(GitPublishState state) => new(state) { PullRequestUrl = "https://github.com/o/r/pull/7" };

        private static PageBuilderController Controller(RecordingRepository repository, FakeGitContentPublisher publisher,
            FakeBlobStorage live = null, FakeGitContentProvisioner provisioner = null, GitRepositorySource source = GitRepositorySource.Site,
            string releaseBranch = null)
        {
            var options = Options.Create(new GitContentOptions
            {
                Enabled = true,
                Repository = "o/r",
                Token = "t0ken",
                BaseBranch = "master",
                ReleaseBranch = releaseBranch,
            });

            // The platform writes the live page itself once a merge is done.
            live ??= new FakeBlobStorage();
            var connection = GitHubConnections.CreateService(options.Value);
            var history = new FakeGitContentHistory();
            // The store's own repository, not the shared one configured above, so a call to the wrong one shows.
            var policy = new FakeGitContentPolicy(source: source, repositoryFormat: "acme/site-{0}");
            var filePageDrafts = TestServices.FilePageDrafts(live, options, connection, policy, repository, history, provisioner ?? new FakeGitContentProvisioner());
            var controller = new PageBuilderController(
                storeService: live.StoreService,
                pathResolver: live,
                blobContentStorageProviderFactory: live,
                publishingService: live,
                eventPublisher: live,
                gitContentOptions: options,
                gitHubConnectionService: connection,
                gitContentPolicy: policy,
                gitContentRepository: repository,
                gitContentHistory: history,
                gitContentPublisher: publisher,
                gitLivePageService: new GitLivePageService(repository, history, connection, live, live, live, live),
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
            designerDiscardValidator: RequestValidators.DesignerDiscard,
            themeStorageFactory: new ThemeStorageFactory(live.StoreService, live, live, live));

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, Login)], "test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            };

            return controller;
        }

        /// <summary>
        /// Behaves like the real repository where it matters here: a branch cut from a ref starts with that
        /// ref's copy of the page, a branch cannot be cut from a ref that does not exist, and deleting a
        /// branch takes its copy away.
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

            /// <summary>
            /// The request is aborted while the commit is in flight: the token is cancelled and the call
            /// throws, the way an HTTP client does. Every method here honours the token it is given, so a
            /// caller that hands the cancelled one to its own cleanup cannot clean up.
            /// </summary>
            public CancellationTokenSource CancelOnCommit { get; set; }

            public HashSet<string> Repositories { get; } = [];

            public Task<string> ReadFileAsync(string repository, string path, string gitRef, CancellationToken cancellationToken = default)
            {
                Repositories.Add(repository);
                return Task.FromResult(Files.GetValueOrDefault((path, gitRef)));
            }

            public Task<string> GetBranchHeadShaAsync(string repository, string branch, CancellationToken cancellationToken = default)
            {
                Repositories.Add(repository);
                return Task.FromResult(BranchHeads.GetValueOrDefault(branch));
            }

            public Task CreateBranchAsync(string repository, string branch, string fromRef, CancellationToken cancellationToken = default)
            {
                var head = BranchHeads.GetValueOrDefault(fromRef) ?? throw new GitContentException($"Could not resolve \"{fromRef}\" to a commit.");
                Repositories.Add(repository);
                Order.Add("create");
                CreatedBranches.Add((branch, fromRef));
                BranchHeads[branch] = head;
                Files[(RepoPath, branch)] = Files.GetValueOrDefault((RepoPath, fromRef));
                return Task.CompletedTask;
            }

            /// <summary>
            /// Moves the ref, and with it what reading the branch answers — the real repository is a
            /// pointer to a commit, and the file on the branch is whatever that commit holds. Here that is
            /// the base branch's copy on the way out, and the draft again on the way back.
            /// </summary>
            public Task SetBranchAsync(string repository, string branch, string sha, string pagePath, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (FailRestore && MovedBranches.Count > 0)
                {
                    throw new HttpRequestException($"could not move \"{branch}\"");
                }

                Repositories.Add(repository);
                Order.Add("move");
                MovedBranches.Add((branch, sha));
                BranchHeads[branch] = sha;

                var at = BranchHeads.FirstOrDefault(head => head.Value == sha && head.Key != branch).Key;
                Files[(pagePath, branch)] = at != null ? Files.GetValueOrDefault((pagePath, at)) : Draft;
                return Task.CompletedTask;
            }

            public Task DeleteBranchAsync(string repository, string branch, string pagePath, CancellationToken cancellationToken = default)
            {
                Repositories.Add(repository);
                Order.Add("delete");
                DeletedBranches.Add(branch);
                BranchHeads.Remove(branch);
                Files.Remove((pagePath, branch));
                return Task.CompletedTask;
            }

            public void InvalidateRead(string repository, string path, string gitRef) => Repositories.Add(repository);

            public Task<string> CommitFileAsync(string repository, string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                if (CancelOnCommit != null)
                {
                    CancelOnCommit.Cancel();
                    throw new OperationCanceledException(CancelOnCommit.Token);
                }

                if (FailNextCommit)
                {
                    FailNextCommit = false;
                    throw new HttpRequestException($"could not commit \"{path}\" to branch \"{branch}\"");
                }

                if (!BranchHeads.ContainsKey(branch))
                {
                    throw new InvalidOperationException($"Cannot commit to \"{branch}\": the branch does not exist.");
                }

                Repositories.Add(repository);
                Order.Add("commit");
                Commits.Add((path, branch, content, message));
                Files[(path, branch)] = content;
                return Task.FromResult(NewCommitSha);
            }

            public Task<string> DeleteFileAsync(string repository, string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                Repositories.Add(repository);
                Files.Remove((path, branch));
                return Task.FromResult(NewCommitSha);
            }
        }
    }
}
