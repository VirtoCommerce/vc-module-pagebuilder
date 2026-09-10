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
    /// Duplicating a page on the git flow.
    /// <para>
    /// The blob copy it replaces always wrote the duplicate as a "-draft" blob, because that is how the
    /// blob flow says "not published yet". With pages in git that produces a file no storefront serves
    /// and no publish ever picks up. Unpublished here means "on a work branch, not on the base branch",
    /// so that is where the copy has to land — which is what most of these pin.
    /// </para>
    /// </summary>
    public class GitCopyPageTests
    {
        private const string Login = "john";
        private const string Base = "dev";

        private const string Published = """{ "settings": { "type": "settings", "name": "About" }, "content": [] }""";
        private const string MyDraft = """{ "settings": { "type": "settings", "name": "About, my edit" }, "content": [] }""";

        private static string BranchOf(string page) =>
            GitPageLocation.BranchFor("designer/{user}/{slug}", Login, GitPageLocation.ContentPath("pages", page));

        [Fact]
        public async Task Copy_LandsOnTheCopiersWorkBranch_NotOnTheBaseBranch()
        {
            // the whole point: a duplicate nobody published must not appear on the base branch, and must
            // not become a "-draft" blob either
            var repository = Repository();

            var result = await Controller(repository).GitCopyPage("vccom", "pages", "/about-us.page", null);

            Assert.IsType<OkObjectResult>(result);
            var commit = Assert.Single(repository.Commits);
            Assert.Equal("pages/about-us_1.page", commit.Path);
            Assert.Equal(BranchOf("/about-us_1.page"), commit.Branch);
            Assert.NotEqual(Base, commit.Branch);
        }

        [Fact]
        public async Task Copy_TakesTheVersionTheEditorIsLookingAt()
        {
            // their own draft of the source, not what production holds — copying the published version
            // would silently duplicate something other than what is on screen
            var repository = Repository();
            repository.Files[("pages/about-us.page", BranchOf("/about-us.page"))] = MyDraft;

            await Controller(repository).GitCopyPage("vccom", "pages", "/about-us.page", null);

            // compared as a document: the commit is written in the canonical form every page in the
            // repository takes, so its bytes differ from the source string by indentation alone
            Assert.True(JToken.DeepEquals(JToken.Parse(Assert.Single(repository.Commits).Content), JToken.Parse(MyDraft)));
        }

        [Fact]
        public async Task Copy_SkipsANameThePublishedBranchAlreadyHas()
        {
            var repository = Repository();
            repository.Files[("pages/about-us_1.page", Base)] = Published;

            await Controller(repository).GitCopyPage("vccom", "pages", "/about-us.page", null);

            Assert.Equal("pages/about-us_2.page", Assert.Single(repository.Commits).Path);
        }

        [Fact]
        public async Task Copy_SkipsANameOnlyAWorkBranchHas()
        {
            // an earlier copy that has not been published lives only on a branch; ignoring that would
            // hand the same name out twice and the second commit would overwrite the first
            var repository = Repository();
            repository.Files[("pages/about-us_1.page", BranchOf("/about-us_1.page"))] = Published;

            await Controller(repository).GitCopyPage("vccom", "pages", "/about-us.page", null);

            Assert.Equal("pages/about-us_2.page", Assert.Single(repository.Commits).Path);
        }

        [Fact]
        public async Task Copy_KeepsTheLanguageSegment()
        {
            // "foo.de.page" is the German page, and its copy has to stay German
            var repository = Repository(source: "/about-us.de.page");

            await Controller(repository).GitCopyPage("vccom", "pages", "/about-us.de.page", null);

            Assert.Equal("pages/about-us_1.de.page", Assert.Single(repository.Commits).Path);
        }

        [Fact]
        public async Task Copy_ASourceNamedByItsDraftPath_CopiesTheSamePage()
        {
            // the blade and the list still hand round the blob draft name for some pages
            var repository = Repository();

            await Controller(repository).GitCopyPage("vccom", "pages", "/about-us.page-draft", null);

            Assert.Equal("pages/about-us_1.page", Assert.Single(repository.Commits).Path);
        }

        [Fact]
        public async Task Copy_ABlogArticle_LandsUnderTheBlogsFolder()
        {
            var repository = new RecordingRepository();
            repository.Files[("pages/blogs/news/hello.page", Base)] = Published;

            await Controller(repository).GitCopyPage("vccom", "blogs", "/news/hello.page", null);

            Assert.Equal("pages/blogs/news/hello_1.page", Assert.Single(repository.Commits).Path);
        }

        [Fact]
        public async Task Copy_WithAnExplicitDestination_UsesIt()
        {
            var repository = Repository();

            await Controller(repository).GitCopyPage("vccom", "pages", "/about-us.page", "/about-us-2026.page");

            Assert.Equal("pages/about-us-2026.page", Assert.Single(repository.Commits).Path);
        }

        [Fact]
        public async Task Copy_WithTheGitFlowOff_IsNotFound_SoTheCallerFallsBackToTheBlobCopy()
        {
            var repository = Repository();

            var result = await Controller(repository, gitFlow: false).GitCopyPage("vccom", "pages", "/about-us.page", null);

            Assert.IsType<NotFoundResult>(result);
            Assert.Empty(repository.Commits);
        }

        [Fact]
        public async Task Copy_WithoutTheCreatePermission_IsForbidden()
        {
            var repository = Repository();

            var result = await Controller(repository, allowed: false).GitCopyPage("vccom", "pages", "/about-us.page", null);

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(repository.Commits);
        }

        [Fact]
        public async Task Copy_OfAPageTheRepositoryDoesNotHave_ShipsNothing()
        {
            var repository = new RecordingRepository();

            var result = await Controller(repository).GitCopyPage("vccom", "pages", "/about-us.page", null);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(repository.Commits);
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static RecordingRepository Repository(string source = "/about-us.page")
        {
            var repository = new RecordingRepository();
            repository.Files[(GitPageLocation.RepoPath("pages", source), Base)] = Published;

            return repository;
        }

        private static PageBuilderController Controller(RecordingRepository repository, bool gitFlow = true, bool allowed = true)
        {
            var options = Options.Create(new GitContentOptions
            {
                Enabled = true,
                Repository = "o/r",
                Token = "t0ken",
                BaseBranch = Base,
                PagesRoot = "pages",
            });

            // Only the git dependencies take part; the rest stay null on purpose, so a change that starts
            // reaching for blob storage fails loudly here instead of quietly writing a draft blob again.
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
                gitContentPublisher: null,
                settingsManager: null,
                authorizationService: new FakeAuthorization(allowed));

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, Login)], "test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            };

            return controller;
        }

        private sealed class RecordingRepository : IGitContentRepository
        {
            public Dictionary<(string Path, string Ref), string> Files { get; } = [];
            public Dictionary<string, string> BranchHeads { get; } = [];
            public List<(string Path, string Content, string Branch)> Commits { get; } = [];

            public Task<string> ReadFileAsync(string path, string gitRef, CancellationToken cancellationToken = default) =>
                Task.FromResult(Files.GetValueOrDefault((path, gitRef)));

            public Task<string> GetBranchHeadShaAsync(string branch, CancellationToken cancellationToken = default) =>
                Task.FromResult(BranchHeads.GetValueOrDefault(branch));

            public Task CreateBranchAsync(string branch, string fromRef, CancellationToken cancellationToken = default)
            {
                BranchHeads[branch] = "abcdef0123456789abcdef0123456789abcdef01";
                return Task.CompletedTask;
            }

            public Task<string> CommitFileAsync(string path, string content, string branch, string message,
                GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                Commits.Add((path, content, branch));
                Files[(path, branch)] = content;
                return Task.FromResult("abcdef0123456789abcdef0123456789abcdef01");
            }

            public Task DeleteBranchAsync(string branch, string pagePath, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public void InvalidateRead(string path, string gitRef) { }

            public Task<string> DeleteFileAsync(string path, string branch, string message, GitCommitAuthor author,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }

        private sealed class SilentHistory : IGitContentHistory
        {
            public Task<GitPageHistory> GetHistoryAsync(string repoPath, GitHistoryQuery query, CancellationToken cancellationToken = default) =>
                Task.FromResult(new GitPageHistory());

            public void Invalidate(string repoPath) { }
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
    }
}
