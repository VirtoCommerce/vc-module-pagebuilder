using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    // VP-9220: GET api/page-builder-pages/grouped/{groupId}/content used to pick the page with the newest
    // ModifiedDate out of the Draft/Published set and stream whatever that page held. Two things went wrong.
    //
    // First, "newest" is not "current". SavePageContent creates a draft row and writes its content in a
    // separate step afterwards, so in between — and permanently, if that write failed — the draft is the
    // newest row while its content column is still NULL.
    //
    // Second, "this page has no content" and "this page's content is empty" were indistinguishable: both
    // produced an empty 200. So a wrong pick was served as a legitimately blank page, and the author, seeing
    // an empty designer, published it — which deleted the page that still held the work.
    //
    // These tests drive the REAL controller action and assert the selection is by status, falls through pages
    // that hold nothing, and reports 404 rather than a blank 200 when the group has no content anywhere.
    public class PageContentSelectionTests
    {
        private const string LiveContent =
            "{ \"content\": [ { \"type\": \"hero\", \"title\": \"Welcome\" } ] }";

        [Fact]
        public async Task Draft_created_but_not_yet_seeded_does_not_shadow_published_content()
        {
            var service = new TestDoubles.FakeGroupedPageService();
            const string groupId = "group-1";

            // Exactly the state SavePageContent leaves behind when the content write never completed: the draft
            // row exists and is the newest page, but no content was ever written for it.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 1) },
                    new() { Id = "page-draft", GroupId = groupId, Status = Draft, ModifiedDate = new DateTime(2026, 7, 22) },
                },
            });
            service.SeedContent("page-published", LiveContent);

            var (body, status) = await GetContent(service, groupId, draft: true);

            Assert.Equal((int)HttpStatusCode.OK, status);
            Assert.Contains("Welcome", body);
        }

        [Fact]
        public async Task Archived_page_is_never_served_even_when_it_is_the_newest_row()
        {
            var service = new TestDoubles.FakeGroupedPageService();
            const string groupId = "group-1";

            // Under the old "newest wins" rule a stale page could out-rank the live one on timestamp alone.
            // Selection by status removes the question: archived pages are not candidates at all.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-archived", GroupId = groupId, Status = Archived, ModifiedDate = new DateTime(2026, 7, 22) },
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 1) },
                },
            });
            service.SeedContent("page-archived", "{ \"content\": [ { \"type\": \"stale\" } ] }");
            service.SeedContent("page-published", LiveContent);

            var (body, status) = await GetContent(service, groupId, draft: true);

            Assert.Equal((int)HttpStatusCode.OK, status);
            Assert.Contains("Welcome", body);
            Assert.DoesNotContain("stale", body);
        }

        [Fact]
        public async Task Page_listed_by_a_stale_group_but_holding_nothing_falls_through_to_published()
        {
            var service = new TestDoubles.FakeGroupedPageService();
            const string groupId = "group-1";

            // A cached group that still lists a draft whose row is already gone. It holds no content, yet it is
            // the newest entry in the stale list and would have won the timestamp comparison.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 1) },
                    new() { Id = "page-deleted", GroupId = groupId, Status = Draft, ModifiedDate = new DateTime(2026, 7, 22) },
                },
            });
            service.SeedContent("page-published", LiveContent);

            var (body, status) = await GetContent(service, groupId, draft: true);

            Assert.Equal((int)HttpStatusCode.OK, status);
            Assert.Contains("Welcome", body);
        }

        [Fact]
        public async Task Group_whose_pages_hold_no_content_reports_not_found_instead_of_a_blank_200()
        {
            var service = new TestDoubles.FakeGroupedPageService();
            const string groupId = "group-1";

            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 1) },
                },
            });

            var (body, status) = await GetContent(service, groupId, draft: true);

            Assert.Equal((int)HttpStatusCode.NotFound, status);
            Assert.Equal(string.Empty, body);
        }

        [Fact]
        public async Task Deliberately_empty_content_is_served_as_empty_not_treated_as_missing()
        {
            var service = new TestDoubles.FakeGroupedPageService();
            const string groupId = "group-1";

            // The distinction the fall-through rests on: a draft saved with empty content is a real answer and
            // must be served, otherwise clearing a page would silently resurrect the published version and the
            // author would think their deletion had not been saved.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-draft", GroupId = groupId, Status = Draft, ModifiedDate = new DateTime(2026, 7, 22) },
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 1) },
                },
            });
            service.SeedContent("page-draft", string.Empty);
            service.SeedContent("page-published", LiveContent);

            var (body, status) = await GetContent(service, groupId, draft: true);

            Assert.Equal((int)HttpStatusCode.OK, status);
            Assert.Equal(string.Empty, body);
        }

        [Fact]
        public async Task Draft_content_wins_over_published_when_the_draft_has_been_seeded()
        {
            var service = new TestDoubles.FakeGroupedPageService();
            const string groupId = "group-1";

            // Guards the other direction: the fall-through must not demote a real draft. The draft is the OLDER
            // row here, so this also pins that selection is by status rather than by timestamp.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-draft", GroupId = groupId, Status = Draft, ModifiedDate = new DateTime(2026, 7, 1) },
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 22) },
                },
            });
            service.SeedContent("page-draft", "{ \"content\": [ { \"type\": \"work-in-progress\" } ] }");
            service.SeedContent("page-published", LiveContent);

            var (body, status) = await GetContent(service, groupId, draft: true);

            Assert.Equal((int)HttpStatusCode.OK, status);
            Assert.Contains("work-in-progress", body);

            // draft:false is the live view and must ignore the draft entirely.
            var (liveBody, liveStatus) = await GetContent(service, groupId, draft: false);

            Assert.Equal((int)HttpStatusCode.OK, liveStatus);
            Assert.Contains("Welcome", liveBody);
        }

        private static async Task<(string Body, int StatusCode)> GetContent(
            TestDoubles.FakeGroupedPageService service, string groupId, bool draft)
        {
            var controller = ControllerFactory.Create(service, out var httpContext);
            var body = new MemoryStream();
            httpContext.Response.Body = body;

            await controller.GetPageContent(groupId, draft, CancellationToken.None);

            body.Position = 0;
            using var reader = new StreamReader(body, Encoding.UTF8);
            return (await reader.ReadToEndAsync(), httpContext.Response.StatusCode);
        }
    }

    internal static class ControllerFactory
    {
        // The 3.830 controller also takes ISettingsManager, IStoreService and IMemberSearchService. None of the
        // content endpoints under test touch them, so they are left null rather than faked into existence.
        internal static PageBuilderPageController Create(
            TestDoubles.FakeGroupedPageService service, out DefaultHttpContext httpContext)
        {
            var controller = new PageBuilderPageController(
                crudService: new TestDoubles.FakePageBuilderPageService(service),
                settingsManager: null,
                groupedPageService: service,
                groupedPageSearchService: new TestDoubles.FakeGroupedPageSearchService(),
                storeService: null,
                authorizationService: new TestDoubles.AllowAllAuthorizationService(),
                pageDocumentSearchService: new TestDoubles.NoopPageDocumentSearchService(),
                memberSearchService: null,
                logger: NullLogger<PageBuilderPageController>.Instance);

            httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            httpContext.Response.Body = new MemoryStream();
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }
    }
}
