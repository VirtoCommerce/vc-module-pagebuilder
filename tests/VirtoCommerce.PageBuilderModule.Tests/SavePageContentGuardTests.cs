using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Common;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    // VP-9220: pages went blank at random for a customer on Azure SQL and the work in them was lost.
    //
    // The reader-side fall-through added for VCST-5417 rescues a page whose content column is NULL — a draft
    // that was created but never seeded. It cannot rescue a page whose column holds a value, because an empty
    // or garbled value is indistinguishable from content the author saved on purpose. So the write path is the
    // last line of defence: once a bad body is committed it shadows the live version, and the next publish
    // promotes it and deletes the page that still had the content.
    //
    // These tests drive the REAL controller action and pin that such a body is refused before anything is
    // written, and — just as important — that legitimate saves still go through untouched.
    public class SavePageContentGuardTests
    {
        private const string LiveContent =
            "{ \"settings\": {}, \"content\": [ { \"type\": \"hero\", \"title\": \"Welcome\" } ] }";

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\r\n")]
        public async Task Empty_body_is_refused_and_leaves_the_published_content_intact(string body)
        {
            var service = NewServiceWithPublishedPage(out var groupId);

            var (result, _) = await SaveContent(service, groupId, body);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(LiveContent, await service.LoadContent("page-published", TestContext.Current.CancellationToken));
        }

        [Theory]
        // A body cut short mid-upload. It is the shape a dropped connection leaves behind, and the shape every
        // reader downstream chokes on — SyncGroupSettingsToContent parses the stored content with JsonNode.
        [InlineData("{ \"settings\": {}, \"content\": [ { \"type\": \"hero\"")]
        [InlineData("not json at all")]
        public async Task Unparsable_body_is_refused_and_leaves_the_published_content_intact(string body)
        {
            var service = NewServiceWithPublishedPage(out var groupId);

            var (result, _) = await SaveContent(service, groupId, body);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(LiveContent, await service.LoadContent("page-published", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Refused_save_does_not_leave_a_draft_row_behind()
        {
            var service = NewServiceWithPublishedPage(out var groupId);

            var (result, _) = await SaveContent(service, groupId, string.Empty);

            Assert.IsType<BadRequestObjectResult>(result);

            // The draft is created before any content is written, so validating after it would strand an
            // unseeded row: the group would read as "Published · has draft with changes" for a save that never
            // happened, and unpublishing would then be refused because the group appears to have changes.
            var group = await service.GetByIdAsync(groupId);
            Assert.DoesNotContain(group.Pages, x => x.Status == Draft);
        }

        [Fact]
        public async Task Valid_content_is_saved_to_a_freshly_created_draft()
        {
            var service = NewServiceWithPublishedPage(out var groupId);
            const string edited =
                "{ \"settings\": {}, \"content\": [ { \"type\": \"hero\", \"title\": \"Edited\" } ] }";

            var (result, _) = await SaveContent(service, groupId, edited);

            Assert.IsType<NoContentResult>(result);

            var group = await service.GetByIdAsync(groupId);
            var draft = Assert.Single(group.Pages, x => x.Status == Draft);
            Assert.Equal(edited, await service.LoadContent(draft.Id, TestContext.Current.CancellationToken));

            // The published page is untouched until the draft is published.
            Assert.Equal(LiveContent, await service.LoadContent("page-published", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Clearing_every_block_is_still_a_legitimate_save()
        {
            var service = NewServiceWithPublishedPage(out var groupId);
            const string cleared = "{ \"settings\": {}, \"content\": [] }";

            // The guard is about payloads that carry no document, not about documents that carry no blocks.
            // Deleting every block and saving is something authors do, and it must keep working.
            var (result, _) = await SaveContent(service, groupId, cleared);

            Assert.IsType<NoContentResult>(result);

            var group = await service.GetByIdAsync(groupId);
            var draft = Assert.Single(group.Pages, x => x.Status == Draft);
            Assert.Equal(cleared, await service.LoadContent(draft.Id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Existing_draft_content_survives_a_refused_save()
        {
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
            const string groupId = "group-1";
            const string draftContent =
                "{ \"settings\": {}, \"content\": [ { \"type\": \"hero\", \"title\": \"Days of work\" } ] }";

            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-draft", GroupId = groupId, Status = Draft, ModifiedDate = new DateTime(2026, 7, 22) },
                },
            });
            service.SeedContent("page-draft", draftContent);

            var (result, _) = await SaveContent(service, groupId, string.Empty);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(draftContent, await service.LoadContent("page-draft", TestContext.Current.CancellationToken));
        }

        private static PublishedRenameContentPreservationTests.FakeGroupedPageService NewServiceWithPublishedPage(
            out string groupId)
        {
            var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
            groupId = "group-1";

            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = "page-published", GroupId = groupId, Status = Published, ModifiedDate = new DateTime(2026, 7, 1) },
                },
            });
            service.SeedContent("page-published", LiveContent);

            return service;
        }

        private static async Task<(IActionResult Result, PageBuilderPageController Controller)> SaveContent(
            PublishedRenameContentPreservationTests.FakeGroupedPageService service, string groupId, string body)
        {
            var controller = new PageBuilderPageController(
                crudService: new PublishedRenameContentPreservationTests.FakePageBuilderPageService(service),
                groupedPageService: service,
                groupedPageSearchService: new PublishedRenameContentPreservationTests.FakeGroupedPageSearchService(),
                authorizationService: new PublishedRenameContentPreservationTests.AllowAllAuthorizationService(),
                pageDocumentSearchService: new PublishedRenameContentPreservationTests.NoopPageDocumentSearchService(),
                eventPublisher: new PublishedRenameContentPreservationTests.NoopEventPublisher(),
                logger: NullLogger<PageBuilderPageController>.Instance);

            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            httpContext.Response.Body = new MemoryStream();
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await controller.SavePageContent(groupId, TestContext.Current.CancellationToken);
            return (result, controller);
        }
    }
}
