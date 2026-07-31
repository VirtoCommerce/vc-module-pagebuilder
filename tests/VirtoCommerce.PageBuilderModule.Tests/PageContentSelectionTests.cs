using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
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
    // VCST-5417 follow-up: GET api/page-builder-pages/grouped/{groupId}/content used to pick the page with the
    // newest ModifiedDate out of a set that mixed Draft, Published and Archived, then stream whatever that page
    // held. Two things went wrong with that.
    //
    // First, "newest" is not "current". UpdateGroup creates a draft row and seeds its content in a *separate*
    // step afterwards, so between the two the newest page is a draft with no content. NormalizePublishedPages
    // demotes superseded pages to Archived in the same save that promotes the new one, so those share a
    // timestamp with the Published page and the tie-break was undefined.
    //
    // Second, "this page has no content" and "this page's content is empty" were indistinguishable: both
    // produced an empty 200. So a wrong pick was served as a legitimately blank page.
    //
    // These tests drive the REAL controller action and assert the selection is by status, falls through pages
    // that hold nothing, and reports 404 rather than a blank 200 when the group truly has no content anywhere.
    public class PageContentSelectionTests
    {
        private const string LiveContent =
            "{ \"content\": [ { \"type\": \"hero\", \"title\": \"Welcome\" } ] }";

        [Fact]
        public async Task Draft_created_but_not_yet_seeded_does_not_shadow_published_content()
        {
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
            const string groupId = "group-1";

            // Exactly the state UpdateGroup leaves behind between SaveChangesAsync and CopyPageContentAsync:
            // the draft row exists and is the newest page, but no content was ever written for it.
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
        public async Task Archived_page_never_wins_over_published_when_timestamps_tie()
        {
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
            const string groupId = "group-1";
            var sameInstant = new DateTime(2026, 7, 22, 10, 0, 0);

            // NormalizePublishedPages demotes and promotes within one save, so both rows carry the same
            // ModifiedDate. The archived page is listed first on purpose: OrderByDescending is a stable sort,
            // so under the old "newest wins" rule the stale archived content was the one served.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-archived", GroupId = groupId, Status = Archived, ModifiedDate = sameInstant },
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = sameInstant },
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
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
            const string groupId = "group-1";

            // A cached group that still lists a page PublishGroup already deleted. The row is gone, so it holds
            // no content, yet it is the newest entry in the stale list.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 1) },
                    new() { Id = "page-deleted", GroupId = groupId, Status = Archived, ModifiedDate = new DateTime(2026, 7, 22) },
                },
            });
            service.SeedContent("page-published", LiveContent);

            var (body, status) = await GetContent(service, groupId, draft: false);

            Assert.Equal((int)HttpStatusCode.OK, status);
            Assert.Contains("Welcome", body);
        }

        [Fact]
        public async Task Group_whose_pages_hold_no_content_reports_not_found_instead_of_a_blank_200()
        {
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
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
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
            const string groupId = "group-1";

            // The distinction the fall-through rests on: a page that was saved with empty content is a real
            // answer and must be served, otherwise clearing a page would silently resurrect an archived version.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 22) },
                    new() { Id = "page-archived", GroupId = groupId, Status = Archived, ModifiedDate = new DateTime(2026, 7, 1) },
                },
            });
            service.SeedContent("page-published", string.Empty);
            service.SeedContent("page-archived", "{ \"content\": [ { \"type\": \"stale\" } ] }");

            var (body, status) = await GetContent(service, groupId, draft: true);

            Assert.Equal((int)HttpStatusCode.OK, status);
            Assert.Equal(string.Empty, body);
        }

        [Fact]
        public async Task Draft_content_wins_over_published_when_the_draft_has_been_seeded()
        {
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
            const string groupId = "group-1";

            // Guards the other direction: the fall-through must not demote a real draft. Note the draft is the
            // OLDER row here, so this also pins that selection is by status rather than by timestamp.
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

        [Fact]
        public async Task Copying_from_a_never_seeded_source_leaves_the_target_unseeded_rather_than_blank()
        {
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();

            // The source draft exists but was never seeded; the target draft holds real content from before.
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = "group-source",
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "source-draft", GroupId = "group-source", Status = Draft, ModifiedDate = new DateTime(2026, 7, 22) },
                },
            });
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = "group-target",
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "target-draft", GroupId = "group-target", Status = Draft, ModifiedDate = new DateTime(2026, 7, 1) },
                },
            });
            service.SeedContent("target-draft", LiveContent);

            var controller = CreateController(service);
            await controller.CopyPageContent("group-target", "group-source", TestContext.Current.CancellationToken);

            // A copy carries the source's state verbatim. Had it written the empty result instead, the target
            // would read as "deliberately empty" and stop falling through to a live page.
            var (body, status) = await GetContent(service, "group-target", draft: true);

            Assert.Equal((int)HttpStatusCode.NotFound, status);
            Assert.Equal(string.Empty, body);
        }

        private static PageBuilderPageController CreateController(
            PublishedRenameContentPreservationTests.FakeGroupedPageService service)
        {
            var pageService = new PublishedRenameContentPreservationTests.FakePageBuilderPageService(service);
            var controller = new PageBuilderPageController(
                crudService: pageService,
                groupedPageService: service,
                groupedPageSearchService: new PublishedRenameContentPreservationTests.FakeGroupedPageSearchService(),
                authorizationService: new PublishedRenameContentPreservationTests.AllowAllAuthorizationService(),
                pageDocumentSearchService: new PublishedRenameContentPreservationTests.NoopPageDocumentSearchService(),
                linkedComponentReferenceIndexService: new NoopLinkedComponentReferenceIndexService(),
                eventPublisher: new PublishedRenameContentPreservationTests.NoopEventPublisher(),
                logger: NullLogger<PageBuilderPageController>.Instance);

            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            httpContext.Response.Body = new MemoryStream();
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static async Task<(string Body, int StatusCode)> GetContent(
            PublishedRenameContentPreservationTests.FakeGroupedPageService service, string groupId, bool draft)
        {
            var pageService = new PublishedRenameContentPreservationTests.FakePageBuilderPageService(service);
            var controller = new PageBuilderPageController(
                crudService: pageService,
                groupedPageService: service,
                groupedPageSearchService: new PublishedRenameContentPreservationTests.FakeGroupedPageSearchService(),
                authorizationService: new PublishedRenameContentPreservationTests.AllowAllAuthorizationService(),
                pageDocumentSearchService: new PublishedRenameContentPreservationTests.NoopPageDocumentSearchService(),
                linkedComponentReferenceIndexService: new NoopLinkedComponentReferenceIndexService(),
                eventPublisher: new PublishedRenameContentPreservationTests.NoopEventPublisher(),
                logger: NullLogger<PageBuilderPageController>.Instance);

            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            var body = new MemoryStream();
            httpContext.Response.Body = body;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            await controller.GetPageContent(groupId, draft);

            body.Position = 0;
            using var reader = new StreamReader(body, Encoding.UTF8);
            return (await reader.ReadToEndAsync(), httpContext.Response.StatusCode);
        }
    }
}
