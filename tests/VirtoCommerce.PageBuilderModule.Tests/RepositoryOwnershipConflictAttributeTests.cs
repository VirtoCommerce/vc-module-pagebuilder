using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// A store working in another store's repository is told so with 409, whichever endpoint asks for its repository:
    /// the endpoints that turn GitHub failures into 400 let this one through to the filter.
    /// </summary>
    public class RepositoryOwnershipConflictAttributeTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public void A_store_working_in_another_stores_repository_is_told_so_with_409()
        {
            var context = Context(new GitRepositoryOwnershipException("acme/site-a", "a"));

            new RepositoryOwnershipConflictAttribute().OnException(context);

            Assert.True(context.ExceptionHandled);
            var conflict = Assert.IsType<ConflictObjectResult>(context.Result);
            Assert.Contains("belongs to store \"a\"", conflict.Value!.GetType().GetProperty("error")!.GetValue(conflict.Value) as string);
        }

        [Fact]
        public void Any_other_failure_goes_on_as_it_was()
        {
            var context = Context(new InvalidOperationException("something else"));

            new RepositoryOwnershipConflictAttribute().OnException(context);

            Assert.False(context.ExceptionHandled);
            Assert.Null(context.Result);
        }

        [Theory]
        [InlineData(typeof(PageBuilderController))]
        [InlineData(typeof(ThemeFileHistoryController))]
        [InlineData(typeof(GroupedPageHistoryController))]
        [InlineData(typeof(GitContentSetupController))]
        public void Every_controller_that_asks_for_a_stores_repository_answers_a_conflict_with_409(Type controller)
        {
            Assert.NotNull(controller.GetCustomAttribute<RepositoryOwnershipConflictAttribute>());
        }

        [Fact]
        public async Task A_theme_files_history_lets_a_conflict_through()
        {
            var live = new FakeLiveStorage();
            var controller = new ThemeFileHistoryController(live, live, live, live, new InProcessDistributedLock(),
                new ThemeFileHistory(Policy(), Repository(), new GitHubScript(), Options(), NullLogger<ThemeFileHistory>.Instance),
                new FakeAuthorizationService(true));

            await Assert.ThrowsAsync<GitRepositoryOwnershipException>(() => controller.GetHistory("s1", "default/templates/product.json", cancellationToken: Ct));
        }

        [Fact]
        public async Task A_database_pages_history_lets_a_conflict_through()
        {
            var group = new GroupedPageBuilderPage { Id = "g1", StoreId = "s1", Name = "Home", Permalink = "/home" };
            var controller = new GroupedPageHistoryController(new OneGroup(group), null, null, null, new FakeAuthorizationService(true),
                new NothingPublished(Policy(), Repository(), new GitHubScript(), Options()))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } },
            };

            await Assert.ThrowsAsync<GitRepositoryOwnershipException>(() => controller.GetHistory("g1", cancellationToken: Ct));
        }

        [Fact]
        public async Task Connecting_a_site_lets_a_conflict_through()
        {
            var controller = new GitContentSetupController(new ConflictingProvisioner(), GitContentConnectionTests.Connection(new MemorySettingsManager()),
                new EphemeralDataProtectionProvider(), new FakeAuthorizationService(true))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            await Assert.ThrowsAsync<GitRepositoryOwnershipException>(() => controller.ConnectSite("s1"));
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static ExceptionContext Context(Exception exception) =>
            new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), []) { Exception = exception };

        private static IOptions<GitContentOptions> Options() => Microsoft.Extensions.Options.Options.Create(new GitContentOptions
        {
            Enabled = true,
            Organization = "acme",
            Token = "t0ken",
            BaseBranch = "main",
            GraphQlUrl = new Uri("https://api.github.test/graphql"),
        });

        // the store's repository in the organization is another store's
        private static GitContentPolicy Policy() => new(Options(), new FakeSettingsManager(true), new FakeGitContentOwnership("another-store"));

        private static GitHubContentRepository Repository() => new(new GitHubScript(), new MemoryCache(new MemoryCacheOptions()), Options());

        private sealed class NothingPublished(IGitContentPolicy policy, IGitContentRepository repository, GitHubScript github, IOptions<GitContentOptions> options)
            : GroupedPageHistory(policy, repository, github, null, options, NullLogger<GroupedPageHistory>.Instance)
        {
            public override Task<string> LoadPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default) =>
                Task.FromResult<string>(null);
        }

        private sealed class OneGroup(GroupedPageBuilderPage group) : IGroupedPageService
        {
            public Task<IList<GroupedPageBuilderPage>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true) =>
                Task.FromResult<IList<GroupedPageBuilderPage>>(ids.Contains(group.Id) ? [group] : []);

            public Task SaveChangesAsync(IList<GroupedPageBuilderPage> models) => throw new NotSupportedException();
            public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
            public Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> TryDeleteEmptyDraftAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private sealed class ConflictingProvisioner : IGitContentProvisioner
        {
            public Task ConnectAsync(string storeId, CancellationToken cancellationToken = default) =>
                throw new GitRepositoryOwnershipException("acme/site-s1", "S1");

            public Task<GitContentSiteStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task CheckOrganizationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task EnsureRepositoryAsync(string storeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task DisconnectAsync(string storeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task ArchiveRepositoryAsync(string storeId, string repository, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }
    }
}
