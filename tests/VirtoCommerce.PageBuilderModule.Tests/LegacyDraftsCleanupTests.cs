using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Models;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// Cleanup of the "-draft" blobs a store carries over when it moves onto the git flow.
    /// <para>
    /// The reason this lives in the page builder at all, rather than the caller just using the content
    /// module's delete: <c>DELETE api/content/{type}/{store}</c> resolves every url it is handed into BOTH
    /// the draft and the published name and removes whichever exist. Handing it "foo.page-draft" therefore
    /// deletes the live "foo.page" as well. Half of what follows pins that this endpoint does not.
    /// </para>
    /// </summary>
    public class LegacyDraftsCleanupTests
    {
        private const string Base = "Pages/vccom/";
        private const string OnGit = """{ "settings": { "type": "settings", "name": "About" }, "content": [] }""";

        // The same document as OnGit, written the way the pre-git flow wrote it: four-space indent, CRLF.
        private const string SameContentLegacyFormatting =
            "{\r\n    \"settings\": {\r\n        \"type\": \"settings\",\r\n        \"name\": \"About\"\r\n    },\r\n    \"content\": []\r\n}\r\n";

        private const string UnpublishedWork = """{ "settings": { "type": "settings", "name": "About, rewritten" }, "content": [] }""";

        // ── inventory ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Inventory_FindsDraftsInSubfolders_AndMapsThemToTheirRepositoryPath()
        {
            var blob = Storage();
            blob.AddFile("about-us.page-draft", OnGit);
            blob.AddFile("blogs/news/hello.page-draft", OnGit);

            var result = await Controller(blob).GetLegacyDrafts("vccom", "pages", "default");

            var items = Items(result);
            Assert.Equal(2, items.Count);
            // blogs live in a subfolder of the pages root and the repository mirrors that, so scanning
            // "pages" is enough to reach them and the path still comes out where the page is committed
            Assert.Equal("pages/about-us.page", Value(items[0], "RepoPath"));
            Assert.Equal("pages/blogs/news/hello.page", Value(items[1], "RepoPath"));
        }

        [Fact]
        public async Task Inventory_IgnoresPublishedPages()
        {
            var blob = Storage();
            blob.AddFile("about-us.page", OnGit);
            blob.AddFile("about-us.page-draft", OnGit);

            var result = await Controller(blob).GetLegacyDrafts("vccom", "pages", "default");

            var item = Assert.Single(Items(result));
            Assert.Equal("about-us.page-draft", Value(item, "BlobPath"));
        }

        [Fact]
        public async Task Inventory_ADraftSayingWhatGitSays_IsNotReportedAsDiffering()
        {
            // The point of the flag. Legacy drafts were written with another indent and CRLF endings, so
            // comparing bytes — which is what publish status does — would mark every one of them changed
            // and the operator would have nothing to go on.
            var blob = Storage();
            blob.AddFile("about-us.page-draft", SameContentLegacyFormatting);

            var result = await Controller(blob, git: Git(("pages/about-us.page", OnGit))).GetLegacyDrafts("vccom", "pages", "default");

            var item = Assert.Single(Items(result));
            Assert.True((bool)Value(item, "ExistsInGit"));
            Assert.False((bool)Value(item, "DiffersFromGit"));
        }

        [Fact]
        public async Task Inventory_ADraftHoldingWorkGitDoesNotHave_IsReportedAsDiffering()
        {
            var blob = Storage();
            blob.AddFile("about-us.page-draft", UnpublishedWork);

            var result = await Controller(blob, git: Git(("pages/about-us.page", OnGit))).GetLegacyDrafts("vccom", "pages", "default");

            var item = Assert.Single(Items(result));
            Assert.True((bool)Value(item, "DiffersFromGit"));
        }

        [Fact]
        public async Task Inventory_AnUnparsableDraft_CountsAsDiffering()
        {
            // "cannot tell" has to fall on the side that keeps the file
            var blob = Storage();
            blob.AddFile("about-us.page-draft", "{ truncated mid-upl");

            var result = await Controller(blob, git: Git(("pages/about-us.page", OnGit))).GetLegacyDrafts("vccom", "pages", "default");

            Assert.True((bool)Value(Assert.Single(Items(result)), "DiffersFromGit"));
        }

        [Fact]
        public async Task Inventory_WithTheGitFlowOff_IsNotFound()
        {
            // there a "-draft" file is the draft, and listing them invites deleting live content
            var blob = Storage();
            blob.AddFile("about-us.page-draft", OnGit);

            var result = await Controller(blob, gitFlow: false).GetLegacyDrafts("vccom", "pages", "default");

            Assert.IsType<NotFoundResult>(result);
        }

        // ── delete ─────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_RemovesTheDraft_AndLeavesThePublishedPageAlone()
        {
            var blob = Storage();
            blob.AddFile("about-us.page", OnGit);
            blob.AddFile("about-us.page-draft", UnpublishedWork);

            await Controller(blob, git: Git(("pages/about-us.page", OnGit)))
                .DeleteLegacyDrafts("vccom", "pages", "default", Request("about-us.page-draft"));

            Assert.Equal(["about-us.page-draft"], blob.Removed);
            Assert.Contains("about-us.page", blob.Files.Keys);
        }

        [Fact]
        public async Task Delete_IsADryRunUnlessItIsToldOtherwise()
        {
            // an omitted flag must be the harmless one: this deletes content that has no second copy
            var blob = Storage();
            blob.AddFile("about-us.page-draft", OnGit);

            var result = await Controller(blob, git: Git(("pages/about-us.page", OnGit)))
                .DeleteLegacyDrafts("vccom", "pages", "default", new LegacyDraftsDeleteRequest { Paths = ["about-us.page-draft"] });

            Assert.Empty(blob.Removed);
            var body = Assert.IsType<OkObjectResult>(result).Value;
            Assert.True((bool)Value(body, "dryRun"));
            Assert.Equal(["about-us.page-draft"], (IEnumerable<string>)Value(body, "deleted"));
        }

        [Theory]
        [InlineData("about-us.page")]
        [InlineData("about-us")]
        [InlineData("")]
        public async Task Delete_APathThatIsNotADraftFile_FailsTheWholeBatch(string path)
        {
            var blob = Storage();
            blob.AddFile("about-us.page", OnGit);
            blob.AddFile("keep-me.page-draft", OnGit);

            var result = await Controller(blob, git: Git(("pages/keep-me.page", OnGit)))
                .DeleteLegacyDrafts("vccom", "pages", "default", Request(path, "keep-me.page-draft"));

            // the valid path in the same batch is not deleted either — a caller with a wrong path has a
            // bug, and a half-finished cleanup is how it stays hidden
            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(blob.Removed);
        }

        [Theory]
        [InlineData("../../Themes/vccom/config/settings_data.json-draft")]
        [InlineData("blogs/../../other-store/about.page-draft")]
        public async Task Delete_RefusesToWalkOutOfTheContentRoot(string path)
        {
            var blob = Storage();

            var result = await Controller(blob).DeleteLegacyDrafts("vccom", "pages", "default", Request(path));

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(blob.Removed);
        }

        [Fact]
        public async Task Delete_ADraftWhosePageIsNotInGit_IsSkippedRatherThanDeleted()
        {
            // GetTemplate still serves that page out of this very blob, so deleting it is not a cleanup:
            // it is the loss of the page
            var blob = Storage();
            blob.AddFile("never-saved.page-draft", UnpublishedWork);

            var result = await Controller(blob, git: Git())
                .DeleteLegacyDrafts("vccom", "pages", "default", Request(false, "never-saved.page-draft"));

            Assert.Empty(blob.Removed);
            var skipped = Assert.Single((IEnumerable<object>)Value(Assert.IsType<OkObjectResult>(result).Value, "skipped"));
            Assert.Equal("never-saved.page-draft", Value(skipped, "path"));
            Assert.Equal("not-in-git", Value(skipped, "reason"));
        }

        [Fact]
        public async Task Delete_WithoutTheDeletePermission_IsForbidden()
        {
            var blob = Storage();
            blob.AddFile("about-us.page-draft", OnGit);

            var result = await Controller(blob, allowed: false)
                .DeleteLegacyDrafts("vccom", "pages", "default", Request(false, "about-us.page-draft"));

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(blob.Removed);
        }

        [Fact]
        public async Task Delete_WithTheGitFlowOff_IsNotFound()
        {
            var blob = Storage();
            blob.AddFile("about-us.page-draft", OnGit);

            var result = await Controller(blob, gitFlow: false)
                .DeleteLegacyDrafts("vccom", "pages", "default", Request(false, "about-us.page-draft"));

            Assert.IsType<NotFoundResult>(result);
            Assert.Empty(blob.Removed);
        }

        // ── the one page the blade has open ────────────────────────────────────────────────────────

        [Fact]
        public async Task Page_WithNoLeftoverDraft_ReportsNothingToDelete()
        {
            var blob = Storage();
            blob.AddFile("about-us.page", OnGit);

            var result = await Controller(blob).GetLegacyDraft("vccom", "pages", "about-us.page", "default");

            Assert.False(Info(result).Exists);
        }

        [Fact]
        public async Task Page_WithALeftoverDraft_ReportsWhetherItStillHoldsAnything()
        {
            var blob = Storage();
            blob.AddFile("about-us.page", OnGit);
            blob.AddFile("about-us.page-draft", UnpublishedWork);

            var result = await Controller(blob, git: Git(("pages/about-us.page", OnGit)))
                .GetLegacyDraft("vccom", "pages", "about-us.page", "default");

            var info = Info(result);
            Assert.True(info.Exists);
            Assert.Equal("about-us.page-draft", info.BlobPath);
            Assert.True(info.ExistsInGit);
            Assert.True(info.DiffersFromGit);
        }

        [Fact]
        public async Task Page_OpenedByItsDraftName_FindsTheSameFile()
        {
            // the list strips the suffix for display but keeps it on the url of a page that only ever had
            // a draft, so the blade can hand over either name
            var blob = Storage();
            blob.AddFile("about-us.page-draft", OnGit);

            var result = await Controller(blob, git: Git(("pages/about-us.page", OnGit)))
                .GetLegacyDraft("vccom", "pages", "about-us.page-draft", "default");

            Assert.Equal("about-us.page-draft", Info(result).BlobPath);
        }

        [Fact]
        public async Task Page_BeingCreated_HasNothingLeftOver()
        {
            var result = await Controller(Storage()).GetLegacyDraft("vccom", "pages", null, "default");

            Assert.False(Info(result).Exists);
        }

        [Fact]
        public async Task Page_WithTheGitFlowOff_IsNotFound()
        {
            var blob = Storage();
            blob.AddFile("about-us.page-draft", OnGit);

            var result = await Controller(blob, gitFlow: false).GetLegacyDraft("vccom", "pages", "about-us.page", "default");

            Assert.IsType<NotFoundResult>(result);
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static LegacyDraftsDeleteRequest Request(params string[] paths) => Request(false, paths);

        private static LegacyDraftsDeleteRequest Request(bool dryRun, params string[] paths) =>
            new() { Paths = paths, DryRun = dryRun };

        private static FakeStorage Storage() => new();

        private static FakeGitRepository Git(params (string Path, string Content)[] files)
        {
            var repository = new FakeGitRepository();
            foreach (var (path, content) in files)
            {
                repository.Files[path] = content;
            }

            return repository;
        }

        private static List<object> Items(ActionResult result) =>
            ((IEnumerable<object>)Value(Assert.IsType<OkObjectResult>(result).Value, "items")).ToList();

        private static LegacyDraftInfo Info(ActionResult result) =>
            Assert.IsType<LegacyDraftInfo>(Assert.IsType<OkObjectResult>(result).Value);

        private static object Value(object body, string property) =>
            body.GetType().GetProperty(property)!.GetValue(body);

        private static PageBuilderController Controller(FakeStorage storage, FakeGitRepository git = null,
            bool gitFlow = true, bool allowed = true)
        {
            var options = Options.Create(new GitContentOptions
            {
                Enabled = true,
                Repository = "o/r",
                Token = "t0ken",
                BaseBranch = "dev",
                PagesRoot = "pages",
            });

            var controller = new PageBuilderController(
                storeService: null,
                pathResolver: new FakePathResolver(),
                blobContentStorageProviderFactory: new FakeStorageFactory(storage),
                publishingService: null,
                eventPublisher: null,
                gitContentOptions: options,
                gitContentPolicy: new FakePolicy(gitFlow),
                gitContentRepository: git ?? new FakeGitRepository(),
                gitContentHistory: null,
                gitContentPublisher: null,
                settingsManager: null,
                authorizationService: new FakeAuthorization(allowed));

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "john")], "test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            };

            return controller;
        }

        private sealed class FakePathResolver : IContentPathResolver
        {
            public string GetContentBasePath(string contentType, string storeId, string themeName = null) => Base;
        }

        private sealed class FakeStorageFactory(FakeStorage storage) : IBlobContentStorageProviderFactory
        {
            public IBlobContentStorageProvider CreateProvider(string basePath) => storage;
        }

        /// <summary>
        /// A flat map of relative url to content, listed back one folder at a time — the way the real
        /// providers list, which is why the endpoint has to walk the tree itself.
        /// </summary>
        private sealed class FakeStorage : IBlobContentStorageProvider
        {
            public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Removed { get; } = [];

            public void AddFile(string relativeUrl, string content) => Files[relativeUrl] = content;

            public Task<BlobEntrySearchResult> SearchAsync(string folderUrl, string keyword)
            {
                var prefix = string.IsNullOrEmpty(folderUrl) ? string.Empty : folderUrl.TrimEnd('/') + "/";
                var results = new List<BlobEntry>();
                var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var path in Files.Keys.Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    var rest = path[prefix.Length..];
                    var slash = rest.IndexOf('/', StringComparison.Ordinal);

                    if (slash < 0)
                    {
                        results.Add(new BlobInfo { Name = rest, RelativeUrl = path, ModifiedDate = DateTime.UtcNow });
                    }
                    else if (folders.Add(rest[..slash]))
                    {
                        results.Add(new BlobFolder { Name = rest[..slash], RelativeUrl = prefix + rest[..slash] });
                    }
                }

                return Task.FromResult(new BlobEntrySearchResult { Results = results, TotalCount = results.Count });
            }

            public Task<Stream> OpenReadAsync(string blobUrl) =>
                Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Files[blobUrl])));

            public Task RemoveAsync(string[] urls)
            {
                foreach (var url in urls)
                {
                    Removed.Add(url);
                    Files.Remove(url);
                }

                return Task.CompletedTask;
            }

            // Nothing else takes part in this path, and a change that starts reaching for it should say so
            // here rather than quietly work against a stub.
            public Task<BlobInfo> GetBlobInfoAsync(string blobUrl) =>
                Task.FromResult(Files.ContainsKey(blobUrl)
                    ? new BlobInfo { Name = blobUrl, RelativeUrl = blobUrl, ModifiedDate = DateTime.UtcNow }
                    : null);

            public Task CreateFolderAsync(BlobFolder folder) => throw new NotSupportedException();
            public Stream OpenRead(string blobUrl) => throw new NotSupportedException();
            public Stream OpenWrite(string blobUrl) => throw new NotSupportedException();
            public Task<Stream> OpenWriteAsync(string blobUrl) => throw new NotSupportedException();
            public void Move(string srcUrl, string destUrl) => throw new NotSupportedException();
            public Task MoveAsyncPublic(string srcUrl, string destUrl) => throw new NotSupportedException();
            public void Copy(string srcUrl, string destUrl) => throw new NotSupportedException();
            public Task CopyAsync(string srcUrl, string destUrl) => throw new NotSupportedException();
            public string GetAbsoluteUrl(string blobKey) => throw new NotSupportedException();
        }

        private sealed class FakeGitRepository : IGitContentRepository
        {
            public Dictionary<string, string> Files { get; } = [];

            public Task<string> ReadFileAsync(string path, string gitRef, CancellationToken cancellationToken = default) =>
                Task.FromResult(Files.GetValueOrDefault(path));

            public Task<string> GetBranchHeadShaAsync(string branch, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task CreateBranchAsync(string branch, string fromRef, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task DeleteBranchAsync(string branch, string pagePath, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public void InvalidateRead(string path, string gitRef) => throw new NotSupportedException();

            public Task<string> CommitFileAsync(string path, string content, string branch, string message,
                GitCommitAuthor author, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<string> DeleteFileAsync(string path, string branch, string message, GitCommitAuthor author,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
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
