using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// "Continue from this version": the chosen commit's content becomes a new commit on the caller's own
    /// work branch. Most of these tests are about what must NOT happen — nothing else in the repository
    /// may move, or restoring a version would destroy the history the version list exists to show.
    /// </summary>
    public class GitRestoreVersionTests
    {
        private const string Login = "john";
        private const string Page = "/about-us.page";
        private const string RepoPath = "pages/about-us.page";
        private const string Sha = "70a3c34b5c6d7e8f90a1b2c3d4e5f60718293a4b";
        private const string OtherBranch = "content/hero-copy";
        private const string ValidPage = """{ "settings": { "type": "settings", "name": "About" }, "content": [] }""";

        private static string MyBranch => GitPageLocation.BranchFor("designer/{user}/{slug}", Login, "about-us.page");

        [Fact]
        public async Task Restore_CommitsTheChosenContentToMyOwnBranch()
        {
            var repository = Repository(ValidPage);
            var history = new RecordingHistory();

            var result = await Controller(repository, history).GitRestoreVersion("vccom", Page, "pages", Sha);

            var body = Assert.IsType<OkObjectResult>(result).Value;
            Assert.Equal(MyBranch, Value(body, "branch"));
            Assert.Equal(RecordingRepository.NewCommitSha, Value(body, "commitSha"));
            Assert.Equal(Sha, Value(body, "restoredFrom"));

            var write = Assert.Single(repository.Commits);
            Assert.Equal(RepoPath, write.Path);
            Assert.Equal(MyBranch, write.Branch);
            Assert.Equal(Login, write.Author.Name);
        }

        [Fact]
        public async Task Restore_NothingButMyOwnBranchIsTouched()
        {
            // the version can come from somebody else's branch: writing into that branch would mean
            // force-pushing over whatever came after the chosen commit, and deleting one would take its
            // versions out of the list
            var repository = Repository(ValidPage);

            await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", Page, "pages", Sha);

            Assert.All(repository.Commits, write => Assert.Equal(MyBranch, write.Branch));
            Assert.Empty(repository.DeletedFiles);
            Assert.Empty(repository.DeletedBranches);
            Assert.DoesNotContain(OtherBranch, repository.CreatedBranches.Select(branch => branch.Branch));
            // the production branch is only ever read, and only as the base my branch is cut from
            Assert.DoesNotContain("master", repository.Commits.Select(write => write.Branch));
        }

        [Fact]
        public async Task Restore_CutsMyBranchFromTheProductionBranchWhenIHaveNoneYet()
        {
            var repository = Repository(ValidPage);

            await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", Page, "pages", Sha);

            var created = Assert.Single(repository.CreatedBranches);
            Assert.Equal(MyBranch, created.Branch);
            Assert.Equal("master", created.FromRef);
        }

        [Fact]
        public async Task Restore_WithADraftAlreadyOpen_AppendsToItInsteadOfCuttingItAgain()
        {
            // my current draft is not lost: it stays in history as the commit before this one
            var repository = Repository(ValidPage);
            repository.BranchHeads[MyBranch] = "1111111111111111111111111111111111111111";

            await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", Page, "pages", Sha);

            Assert.Empty(repository.CreatedBranches);
            Assert.Single(repository.Commits);
        }

        [Fact]
        public async Task Restore_WritesCanonicalBytesRatherThanWhatTheOldCommitHeld()
        {
            // an older version may predate the canonical form; re-committing it verbatim would make
            // publish-status report changes that are only line endings and indentation
            var repository = Repository("{\r\n    \"settings\": { \"type\": \"settings\" },\r\n    \"content\": []\r\n}");

            await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", Page, "pages", Sha);

            var committed = Assert.Single(repository.Commits).Content;
            Assert.DoesNotContain("\r\n", committed);
            Assert.Equal(PageJson.Serialize(JToken.Parse(committed)), committed);
        }

        [Fact]
        public async Task Restore_RecordsWhoDidItInAWayTheClientCannotSet()
        {
            var repository = Repository(ValidPage);

            await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", Page, "pages", Sha);

            var message = Assert.Single(repository.Commits).Message;
            Assert.Contains("restore /about-us.page from 70a3c34", message);
            Assert.Equal(Login, GitCommitMessage.ReadTrailer(message, GitCommitMessage.VcUserTrailer));
        }

        [Fact]
        public async Task Restore_DropsTheCachedVersionList()
        {
            var repository = Repository(ValidPage);
            var history = new RecordingHistory();

            await Controller(repository, history).GitRestoreVersion("vccom", Page, "pages", Sha);

            Assert.Equal([RepoPath], history.Invalidated);
        }

        [Fact]
        public async Task Restore_UnknownCommit_IsNotFoundAndWritesNothing()
        {
            var repository = new RecordingRepository();

            var result = await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", Page, "pages", Sha);

            Assert.IsType<NotFoundObjectResult>(result);
            AssertNothingWritten(repository);
        }

        [Fact]
        public async Task Restore_AVersionTheDeployWouldRefuse_IsRejectedBeforeAnythingIsWritten()
        {
            // restoring a page CI cannot validate would leave the editor holding a branch that cannot be
            // published — and, if the branch were cut first, an unseeded one behind it
            var repository = Repository("not a page at all");

            var result = await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", Page, "pages", Sha);

            Assert.IsType<BadRequestObjectResult>(result);
            AssertNothingWritten(repository);
        }

        [Theory]
        [InlineData(OtherBranch)]
        [InlineData("master")]
        [InlineData("70a3c34")]
        [InlineData(null)]
        public async Task Restore_ARefThatIsNotACommitSha_IsRefused(string gitRef)
        {
            // a branch name means "whatever is there when this runs", which is not a version
            var repository = Repository(ValidPage);

            var result = await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", Page, "pages", gitRef);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(repository.Reads);
            AssertNothingWritten(repository);
        }

        [Fact]
        public async Task Restore_WithTheGitFlowOff_IsNotFound()
        {
            var repository = Repository(ValidPage);

            var result = await Controller(repository, new RecordingHistory(), gitFlow: false).GitRestoreVersion("vccom", Page, "pages", Sha);

            Assert.IsType<NotFoundResult>(result);
            AssertNothingWritten(repository);
        }

        [Fact]
        public async Task Restore_AContentTypeGitDoesNotCover_IsNotFound()
        {
            var repository = Repository(ValidPage);

            var result = await Controller(repository, new RecordingHistory()).GitRestoreVersion("vccom", "default/config.json", "themes", Sha);

            Assert.IsType<NotFoundResult>(result);
            AssertNothingWritten(repository);
        }

        [Fact]
        public async Task Preview_OfAVersion_RedirectsToThatExactCommit()
        {
            var repository = new RecordingRepository();
            repository.BranchHeads[MyBranch] = "1111111111111111111111111111111111111111";

            var result = await Controller(repository, new RecordingHistory()).GitPreview("vccom", Page, "pages", Sha);

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Contains($"ref={Sha}", redirect.Url);
            // a version was asked for by sha, so the editor's own draft must not be resolved instead
            Assert.DoesNotContain("1111111", redirect.Url);
        }

        [Fact]
        public async Task Preview_ARefThatIsNotACommitSha_IsRefused()
        {
            // the ref goes straight into a redirect to the storefront; a caller-chosen branch name there
            // would stop the link meaning what it said
            var result = await Controller(new RecordingRepository(), new RecordingHistory()).GitPreview("vccom", Page, "pages", OtherBranch);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Preview_WithoutTheModuleSetting_UsesTheStoresOwnStorefrontUrl()
        {
            // the module setting is one value for the whole installation, so on a multi-store platform it
            // can only be right about one store; the store itself records where its storefront is
            var store = new Store { Id = "vccom", Url = "https://vccom.example" };
            var controller = Controller(new RecordingRepository(), new RecordingHistory(), configuredStoreUrl: null, store: store);

            var result = await controller.GitPreview("vccom", Page, "pages", Sha);

            Assert.StartsWith("https://vccom.example/designer-preview?", Assert.IsType<RedirectResult>(result).Url);
        }

        [Fact]
        public async Task Preview_PrefersTheStoresSecureUrl()
        {
            // the platform is served over https, and a preview that redirects to http is a dead end
            var store = new Store { Id = "vccom", Url = "http://vccom.example", SecureUrl = "https://secure.vccom.example" };
            var controller = Controller(new RecordingRepository(), new RecordingHistory(), configuredStoreUrl: null, store: store);

            var result = await controller.GitPreview("vccom", Page, "pages", Sha);

            Assert.StartsWith("https://secure.vccom.example/", Assert.IsType<RedirectResult>(result).Url);
        }

        [Fact]
        public async Task Preview_TheModuleSettingOverridesTheStore()
        {
            // deliberately set to somewhere else — a staging host or a tunnel — and that choice wins
            var store = new Store { Id = "vccom", Url = "https://vccom.example" };
            var controller = Controller(new RecordingRepository(), new RecordingHistory(), configuredStoreUrl: "https://tunnel.test/", store: store);

            var result = await controller.GitPreview("vccom", Page, "pages", Sha);

            Assert.StartsWith("https://tunnel.test/designer-preview?", Assert.IsType<RedirectResult>(result).Url);
        }

        [Fact]
        public async Task Preview_NoUrlAnywhere_SaysWhatToFillIn()
        {
            var controller = Controller(new RecordingRepository(), new RecordingHistory(),
                configuredStoreUrl: null, store: new Store { Id = "vccom" });

            var result = await controller.GitPreview("vccom", Page, "pages", Sha);

            var error = Assert.IsType<BadRequestObjectResult>(result).Value.ToString();
            Assert.Contains("has no storefront url", error);
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static RecordingRepository Repository(string contentAtSha)
        {
            var repository = new RecordingRepository();
            repository.Files[(RepoPath, Sha)] = contentAtSha;
            return repository;
        }

        private static void AssertNothingWritten(RecordingRepository repository)
        {
            Assert.Empty(repository.Commits);
            Assert.Empty(repository.CreatedBranches);
            Assert.Empty(repository.DeletedBranches);
            Assert.Empty(repository.DeletedFiles);
        }

        private static string Value(object body, string property) =>
            body.GetType().GetProperty(property)!.GetValue(body) as string;

        private static PageBuilderController Controller(RecordingRepository repository, RecordingHistory history, bool gitFlow = true,
            string configuredStoreUrl = "https://store.test", Store store = null)
        {
            var options = Options.Create(new GitContentOptions
            {
                Enabled = true,
                Repository = "o/r",
                Token = "t0ken",
                BaseBranch = "master",
            });

            // Only the git dependencies take part in these paths; the rest are left null on purpose, so a
            // change that starts reaching for blob storage or the publisher fails loudly here.
            var controller = new PageBuilderController(
                storeService: new FakeStoreService(store),
                pathResolver: null,
                blobContentStorageProviderFactory: null,
                publishingService: null,
                eventPublisher: null,
                gitContentOptions: options,
                gitContentPolicy: new FakePolicy(gitFlow),
                gitContentRepository: repository,
                gitContentHistory: history,
                gitContentPublisher: null,
                settingsManager: new PerNameSettings(configuredStoreUrl),
                authorizationService: null);

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

        private sealed class RecordingHistory : IGitContentHistory
        {
            public List<string> Invalidated { get; } = [];

            public Task<GitPageHistory> GetHistoryAsync(string repoPath, GitHistoryQuery query, CancellationToken cancellationToken = default) =>
                Task.FromResult(new GitPageHistory());

            public void Invalidate(string repoPath) => Invalidated.Add(repoPath);
        }

        private sealed class RecordingRepository : IGitContentRepository
        {
            public const string NewCommitSha = "abcdef0123456789abcdef0123456789abcdef01";

            public Dictionary<(string Path, string Ref), string> Files { get; } = [];
            public Dictionary<string, string> BranchHeads { get; } = [];

            public List<(string Path, string Ref)> Reads { get; } = [];
            public List<(string Path, string Content, string Branch, string Message, GitCommitAuthor Author)> Commits { get; } = [];
            public List<(string Branch, string FromRef)> CreatedBranches { get; } = [];
            public List<string> DeletedBranches { get; } = [];
            public List<(string Path, string Branch)> DeletedFiles { get; } = [];

            public Task<string> ReadFileAsync(string path, string gitRef, CancellationToken cancellationToken = default)
            {
                Reads.Add((path, gitRef));
                return Task.FromResult(Files.GetValueOrDefault((path, gitRef)));
            }

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
                return Task.CompletedTask;
            }

            public void InvalidateRead(string path, string gitRef) { }

            public Task<string> CommitFileAsync(string path, string content, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                Commits.Add((path, content, branch, message, author));
                return Task.FromResult(NewCommitSha);
            }

            public Task<string> DeleteFileAsync(string path, string branch, string message, GitCommitAuthor author, CancellationToken cancellationToken = default)
            {
                DeletedFiles.Add((path, branch));
                return Task.FromResult(NewCommitSha);
            }
        }
        /// <summary>
        /// Answers with one store, and nothing else: a change that starts using the store service for
        /// something other than looking a store up fails here rather than passing quietly.
        /// </summary>
        private sealed class FakeStoreService(Store store) : IStoreService
        {
            public Task<IList<Store>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true) =>
                Task.FromResult<IList<Store>>(store != null && ids.Contains(store.Id) ? [store] : []);

            public Task<IList<Store>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true) =>
                throw new NotSupportedException();

            public Task SaveChangesAsync(IList<Store> models) => throw new NotSupportedException();

            public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();

            public Task<IList<string>> GetUserAllowedStoreIdsAsync(ApplicationUser user) => throw new NotSupportedException();
        }
        /// <summary>
        /// Answers per setting name, unlike the shared fake which returns one value for every lookup —
        /// the preview url is assembled from two settings, and a fake that conflates them hides that.
        /// </summary>
        private sealed class PerNameSettings(string storeUrl) : ISettingsManager
        {
            public Task<ObjectSettingEntry> GetObjectSettingAsync(string name, string objectType = null, string objectId = null)
            {
                var value = name == ModuleConstants.Settings.General.StoreUrl.Name ? storeUrl : null;
                return Task.FromResult(new ObjectSettingEntry { Name = name, Value = value });
            }

            public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null) =>
                throw new NotSupportedException();

            public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => throw new NotSupportedException();

            public Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => throw new NotSupportedException();

            public IEnumerable<SettingDescriptor> AllRegisteredSettings => throw new NotSupportedException();

            public IEnumerable<SettingDescriptor> GetSettingsForType(string typeName) => throw new NotSupportedException();

            public IDictionary<string, string[]> GetSettingTypeAssignments() => throw new NotSupportedException();

            public void RegisterSettings(IEnumerable<SettingDescriptor> settings, string moduleId = null) => throw new NotSupportedException();

            public void RegisterSettingsForType(IEnumerable<SettingDescriptor> settings, string typeName) => throw new NotSupportedException();
        }
    }
}