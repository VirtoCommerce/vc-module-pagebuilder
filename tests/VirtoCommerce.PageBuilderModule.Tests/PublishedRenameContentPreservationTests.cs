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
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Pages.Core.Models;
using VirtoCommerce.Pages.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    // Reproduction for VCST-5417: renaming a *Published* Page Builder page (Name/Permalink)
    // via the details blade -> Save (PUT grouped) -> Publish (POST grouped/publishing/{id})
    // must carry the live content blocks into the newly published version. The report says the
    // content is lost and the published page ends up empty.
    //
    // This test drives the REAL PageBuilderPageController (UpdateGroup + PublishGroup) against a
    // faithful in-memory IGroupedPageService that models the documented semantics:
    //   * per-page content storage (a page id -> content string map),
    //   * GetAsync(clone: true) returns a DETACHED deep copy (as the platform CrudService does),
    //     so controller mutations only persist on SaveChangesAsync,
    //   * SaveChangesAsync assigns ids to new pages and enforces one-Published-per-group
    //     (NormalizePublishedPages: the page transitioning to Published this save wins, the rest
    //     are demoted to Archived), mirroring GroupedPageService.BeforeSaveChanges,
    //   * CopyPageContentAsync deep-copies content between page ids,
    //   * DeleteAsync removes page rows (as PublishGroup does after promotion).
    //
    // The fake is intentionally faithful (no trivial pass-by-construction): content lives per page id,
    // a brand-new draft starts EMPTY, and only an explicit CopyPageContentAsync moves content into it.
    public class PublishedRenameContentPreservationTests
    {
        private const string OriginalContent =
            "{ \"settings\": { \"name\": \"Landing\", \"permalink\": \"/landing\" }, " +
            "\"content\": [ { \"type\": \"hero\", \"title\": \"Welcome\" }, { \"type\": \"text\", \"html\": \"body copy\" } ] }";

        [Fact]
        public async Task Rename_published_page_save_then_publish_preserves_content_across_two_cycles()
        {
            var service = new FakeGroupedPageService();

            // Seed: a group with a single Published page that has real content blocks (the live page).
            var groupId = "group-1";
            var publishedPageId = "page-published";
            service.SeedGroup(new GroupedPageBuilderPage
            {
                Id = groupId,
                Name = "Landing",
                Permalink = "/landing",
                StoreId = "Store1",
                Pages = new List<PageBuilderPage>
                {
                    new() { Id = publishedPageId, GroupId = groupId, StoreId = "Store1", Status = Published },
                },
            });
            service.SeedContent(publishedPageId, OriginalContent);

            var controller = CreateController(service);

            // ---- Cycle 1: rename -> Save -> Publish ----
            await RenameSaveAndPublish(controller, service, groupId, newName: "Landing v2", newPermalink: "/landing-2");

            var afterCycle1 = await GetPublishedContent(service, groupId);
            AssertContentBlocksPreserved(afterCycle1, "after first rename+publish cycle");
            Assert.Contains("Landing v2", afterCycle1); // metadata was synced into settings
            Assert.Contains("/landing-2", afterCycle1);

            // ---- Cycle 2: rename again -> Save -> Publish ----
            await RenameSaveAndPublish(controller, service, groupId, newName: "Landing v3", newPermalink: "/landing-3");

            var afterCycle2 = await GetPublishedContent(service, groupId);
            AssertContentBlocksPreserved(afterCycle2, "after second rename+publish cycle");
            Assert.Contains("Landing v3", afterCycle2);
            Assert.Contains("/landing-3", afterCycle2);
        }

        private static void AssertContentBlocksPreserved(string content, string when)
        {
            Assert.False(string.IsNullOrWhiteSpace(content), $"Published content is empty {when} (content was lost).");
            Assert.Contains("\"hero\"", content);
            Assert.Contains("Welcome", content);
            Assert.Contains("body copy", content);
        }

        private static async Task RenameSaveAndPublish(
            PageBuilderPageController controller,
            FakeGroupedPageService service,
            string groupId,
            string newName,
            string newPermalink)
        {
            // Save: the blade PUTs the current group with the edited Name/Permalink.
            var current = await service.GetByIdAsync(groupId);
            current.Name = newName;
            current.Permalink = newPermalink;

            var saveResult = await controller.UpdateGroup(current);
            Assert.IsType<OkObjectResult>(saveResult.Result);

            // Publish: promote the freshly created draft to Published.
            var publishResult = await controller.PublishGroup(groupId, publish: true);
            Assert.IsType<OkResult>(publishResult);
        }

        // Mirrors GetPageContent: pick the newest page (published wins here after publish) and read its content.
        private static async Task<string> GetPublishedContent(FakeGroupedPageService service, string groupId)
        {
            var group = await service.GetByIdAsync(groupId);
            var pageId = group.Pages
                .Where(x => x.Status == Published || x.Status == Archived)
                .OrderByDescending(x => x.ModifiedDate)
                .Select(x => x.Id)
                .FirstOrDefault();
            Assert.NotNull(pageId);
            return await service.LoadContent(pageId!);
        }

        private static PageBuilderPageController CreateController(FakeGroupedPageService service)
        {
            var pageService = new FakePageBuilderPageService(service);
            var pageContentService = new PageBuilderPageContentService(
                pageService,
                service,
                new NoopSharedComponentReferenceIndexService(),
                new NoopEventPublisher(),
                NullLogger<PageBuilderPageContentService>.Instance);
            var controller = new PageBuilderPageController(
                crudService: pageService,
                groupedPageService: service,
                groupedPageSearchService: new FakeGroupedPageSearchService(),
                authorizationService: new AllowAllAuthorizationService(),
                pageDocumentSearchService: new NoopPageDocumentSearchService(),
                pageContentService: pageContentService,
                logger: NullLogger<PageBuilderPageController>.Instance);

            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            httpContext.Response.Body = new MemoryStream();
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        // ---------------------------------------------------------------------------------------
        // Faithful in-memory IGroupedPageService.
        // ---------------------------------------------------------------------------------------
        internal sealed class FakeGroupedPageService : IGroupedPageService
        {
            private readonly Dictionary<string, GroupedPageBuilderPage> _groups = new();
            private readonly Dictionary<string, string> _content = new();
            private int _idSeq;

            internal Exception SaveContentException { get; set; }
            internal string ConcurrentContentBeforeSaveFailure { get; set; }
            internal int EmptyDraftCleanupAttempts { get; private set; }
            internal string ReplaceNewDraftAfterSaveWithPageId { get; set; }

            public void SeedGroup(GroupedPageBuilderPage group) => _groups[group.Id] = DeepClone(group);
            public void SeedContent(string pageId, string content) => _content[pageId] = content;

            public Task<IList<GroupedPageBuilderPage>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            {
                IList<GroupedPageBuilderPage> result = ids
                    .Where(id => id != null && _groups.ContainsKey(id))
                    .Select(id => clone ? DeepClone(_groups[id]) : _groups[id])
                    .ToList();
                return Task.FromResult(result);
            }

            public Task SaveChangesAsync(IList<GroupedPageBuilderPage> models)
            {
                foreach (var model in models)
                {
                    if (string.IsNullOrEmpty(model.Id))
                    {
                        model.Id = $"group-new-{++_idSeq}";
                    }

                    foreach (var page in model.Pages)
                    {
                        if (string.IsNullOrEmpty(page.Id))
                        {
                            page.Id = $"page-new-{++_idSeq}";
                        }
                        page.GroupId = model.Id;
                        page.ModifiedDate = DateTime.UtcNow.AddTicks(++_idSeq);
                    }

                    NormalizePublishedPages(model);

                    _groups[model.Id] = DeepClone(model);
                    if (!string.IsNullOrWhiteSpace(ReplaceNewDraftAfterSaveWithPageId))
                    {
                        var storedDraft = _groups[model.Id].Pages.FirstOrDefault(x => x.Status == Draft);
                        if (storedDraft != null)
                        {
                            storedDraft.Id = ReplaceNewDraftAfterSaveWithPageId;
                        }

                        ReplaceNewDraftAfterSaveWithPageId = null;
                    }
                }

                return Task.CompletedTask;
            }

            // Faithful mirror of GroupedPageService.NormalizePublishedPages: keep the page that is
            // transitioning to Published in this save (compared to stored state), demote the rest.
            private void NormalizePublishedPages(GroupedPageBuilderPage group)
            {
                var publishedPages = group.Pages.Where(x => x.Status == Published).ToList();
                if (publishedPages.Count <= 1)
                {
                    return;
                }

                PageBuilderPage keep = null;
                if (!string.IsNullOrEmpty(group.Id) && _groups.TryGetValue(group.Id, out var existing))
                {
                    var existingStatusById = existing.Pages
                        .Where(p => !string.IsNullOrEmpty(p.Id))
                        .ToDictionary(p => p.Id, p => p.Status);

                    var newlyPromoted = publishedPages
                        .Where(p => !string.IsNullOrEmpty(p.Id)
                            && existingStatusById.TryGetValue(p.Id, out var oldStatus)
                            && oldStatus != Published)
                        .ToList();

                    if (newlyPromoted.Count == 1)
                    {
                        keep = newlyPromoted[0];
                    }
                }

                keep ??= publishedPages.OrderByDescending(x => x.CreatedDate).First();

                foreach (var page in publishedPages.Where(p => p.Id != keep.Id))
                {
                    page.Status = Archived;
                }
            }

            public Task DeleteAsync(IList<string> ids, bool softDelete = false)
            {
                foreach (var id in ids)
                {
                    _content.Remove(id);
                    foreach (var group in _groups.Values)
                    {
                        var page = group.Pages.FirstOrDefault(p => p.Id == id);
                        if (page != null)
                        {
                            group.Pages.Remove(page);
                        }
                    }
                }
                return Task.CompletedTask;
            }

            public Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default)
                => Task.FromResult(_content.TryGetValue(pageId, out var c) ? c : string.Empty);

            public Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default)
            {
                if (SaveContentException != null)
                {
                    if (ConcurrentContentBeforeSaveFailure != null)
                    {
                        _content[pageId] = ConcurrentContentBeforeSaveFailure;
                    }

                    throw SaveContentException;
                }

                _content[pageId] = content;
                return Task.CompletedTask;
            }

            // Absence from _content mirrors a NULL PageContent column — content was never written for this page.
            // A present empty string mirrors '' — content was written and is deliberately empty. Only the former
            // reports false, and only the former writes nothing to the stream.
            public async Task<bool> LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
            {
                if (!_content.TryGetValue(pageId, out var content))
                {
                    return false;
                }

                var bytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(bytes, cancellationToken);
                return true;
            }

            public async Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
                _content[pageId] = await reader.ReadToEndAsync(cancellationToken);
            }

            public Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default)
            {
                // Mirrors the server-side copy: the target takes on exactly the source's state. A source that was
                // never seeded leaves the target unseeded too, rather than "deliberately empty".
                if (_content.TryGetValue(sourcePageId, out var c))
                {
                    _content[targetPageId] = c;
                }
                else
                {
                    _content.Remove(targetPageId);
                }

                return Task.CompletedTask;
            }

            public async Task<bool> TryDeleteEmptyDraftAsync(
                string pageId,
                CancellationToken cancellationToken = default)
            {
                EmptyDraftCleanupAttempts++;
                var page = GetStoredPage(pageId);
                if (page == null || page.Status != Draft || _content.ContainsKey(pageId))
                {
                    return false;
                }

                await DeleteAsync([pageId]);
                return true;
            }

            internal PageBuilderPage GetStoredPage(string pageId)
                => _groups.Values.SelectMany(g => g.Pages).FirstOrDefault(p => p.Id == pageId);

            private static GroupedPageBuilderPage DeepClone(GroupedPageBuilderPage group)
            {
                return new GroupedPageBuilderPage
                {
                    Id = group.Id,
                    Name = group.Name,
                    Permalink = group.Permalink,
                    CultureName = group.CultureName,
                    StoreId = group.StoreId,
                    Visibility = group.Visibility,
                    UserGroups = group.UserGroups,
                    StartDate = group.StartDate,
                    EndDate = group.EndDate,
                    OrganizationId = group.OrganizationId,
                    CreatedDate = group.CreatedDate,
                    ModifiedDate = group.ModifiedDate,
                    Pages = group.Pages.Select(p => new PageBuilderPage
                    {
                        Id = p.Id,
                        GroupId = p.GroupId,
                        StoreId = p.StoreId,
                        Status = p.Status,
                        CreatedDate = p.CreatedDate,
                        ModifiedDate = p.ModifiedDate,
                    }).ToList(),
                };
            }
        }

        internal sealed class FakePageBuilderPageService(FakeGroupedPageService groupedService) : IPageBuilderPageService
        {
            public Task<IList<PageBuilderPage>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            {
                IList<PageBuilderPage> result = ids
                    .Select(groupedService.GetStoredPage)
                    .Where(p => p != null)
                    .ToList();
                return Task.FromResult(result);
            }

            public Task SaveChangesAsync(IList<PageBuilderPage> models) => Task.CompletedTask;

            public Task DeleteAsync(IList<string> ids, bool softDelete = false) => groupedService.DeleteAsync(ids, softDelete);
        }

        internal sealed class FakeGroupedPageSearchService : IGroupedPageSearchService
        {
            public Task<GroupedPageBuilderPageSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria, bool clone = true)
                => Task.FromResult(new GroupedPageBuilderPageSearchResult());
        }

        internal sealed class NoopPageDocumentSearchService : IPageDocumentSearchService
        {
            public Task<IndexingResult> IndexDocuments(PageDocument[] documents) => Task.FromResult(new IndexingResult());
            public Task<IndexingResult> RemoveDocuments(string[] documentIds) => Task.FromResult(new IndexingResult());
            public Task<PageDocumentSearchResult> SearchAsync(PageDocumentSearchCriteria criteria, bool clone = true)
                => Task.FromResult(new PageDocumentSearchResult());
        }

        internal sealed class NoopEventPublisher : IEventPublisher
        {
            public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : IEvent
                => Task.CompletedTask;
        }

        internal sealed class AllowAllAuthorizationService : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
                => Task.FromResult(AuthorizationResult.Success());

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
                => Task.FromResult(AuthorizationResult.Success());
        }
    }
}
