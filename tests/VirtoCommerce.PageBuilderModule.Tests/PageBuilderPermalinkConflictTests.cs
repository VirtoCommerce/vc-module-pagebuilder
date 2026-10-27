using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Common;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderPermalinkConflictTests
{
    [Fact]
    public void GetConflict_SameAudience_IsCertain()
    {
        Assert.Equal(PageBuilderPermalinkConflict.Certain, PageBuilderPermalinkConflicts.GetConflict(Group(), PublishedGroup()));
    }

    [Theory]
    [InlineData("/about", "about")]
    [InlineData("/About", "/about")]
    [InlineData(" /about", "/about")]
    public void GetConflict_PermalinkDiffersOnlyInCaseOrSlash_IsCertain(string permalink, string otherPermalink)
    {
        var page = Group(x => x.Permalink = permalink);
        var other = PublishedGroup(x => x.Permalink = otherPermalink);

        Assert.Equal(PageBuilderPermalinkConflict.Certain, PageBuilderPermalinkConflicts.GetConflict(page, other));
    }

    [Fact]
    public void GetConflict_OtherPageIsOver_IsNoneOnlyAfterItsEnd()
    {
        var other = PublishedGroup(x =>
        {
            x.StartDate = Date(1);
            x.EndDate = Date(31);
        });

        Assert.Equal(PageBuilderPermalinkConflict.Certain, PageBuilderPermalinkConflicts.GetConflict(Group(), other, Date(15)));
        Assert.Equal(PageBuilderPermalinkConflict.None, PageBuilderPermalinkConflicts.GetConflict(Group(), other, Date(31).AddDays(1)));
    }

    [Fact]
    public void GetConflict_OtherIsDraft_IsNone()
    {
        var other = Group(x => x.Pages = [new PageBuilderPage { Id = "draft", Status = Draft }]);

        Assert.Equal(PageBuilderPermalinkConflict.None, PageBuilderPermalinkConflicts.GetConflict(Group(), other));
    }

    [Fact]
    public void GetConflict_OtherHasPublishedPageAndDraft_IsCertain()
    {
        var other = Group(x => x.Pages =
        [
            new PageBuilderPage { Id = "published", Status = Published },
            new PageBuilderPage { Id = "draft", Status = Draft },
        ]);

        Assert.Equal(PageBuilderPermalinkConflict.Certain, PageBuilderPermalinkConflicts.GetConflict(Group(), other));
    }

    public static TheoryData<string, Action<GroupedPageBuilderPage>> NotConflicting => new()
    {
        { "other permalink", x => x.Permalink = "/contacts" },
        { "other store", x => x.StoreId = "other-store" },
        { "other language", x => x.CultureName = "de-DE" },
        { "other organization", x => x.OrganizationId = "org-b" },
        // The storefront prefers the more specific page, so a general page next to it is intended.
        { "any language", x => x.CultureName = null },
        { "any organization", x => x.OrganizationId = null },
        { "other visibility", x => x.Visibility = true },
        { "for user groups", x => x.UserGroups = "vip" },
        { "ends before", x => { x.StartDate = Date(1); x.EndDate = Date(9); } },
        { "starts after", x => x.StartDate = Date(21) },
    };

    [Theory]
    [MemberData(nameof(NotConflicting))]
    public void GetConflict_NotOfferedTogetherOrNotEquallySpecific_IsNone(string difference, Action<GroupedPageBuilderPage> changeOther)
    {
        var page = Group(x =>
        {
            x.OrganizationId = "org-a";
            x.StartDate = Date(10);
            x.EndDate = Date(20);
        });
        var other = PublishedGroup(x =>
        {
            x.OrganizationId = "org-a";
            changeOther(x);
        });

        Assert.True(PageBuilderPermalinkConflicts.GetConflict(page, other, Now) == PageBuilderPermalinkConflict.None, difference);
    }

    public static TheoryData<string, Action<GroupedPageBuilderPage>> Conflicting => new()
    {
        { "same page settings", _ => { } },
        { "language in another case", x => x.CultureName = "EN-us" },
        { "no dates", x => { x.StartDate = null; x.EndDate = null; } },
        { "dates overlap", x => { x.StartDate = Date(15); x.EndDate = Date(30); } },
        { "ends on the start day", x => { x.StartDate = Date(1); x.EndDate = Date(10); } },
    };

    [Theory]
    [MemberData(nameof(Conflicting))]
    public void GetConflict_EquallySpecificAndOfferedTogether_IsCertain(string difference, Action<GroupedPageBuilderPage> changeOther)
    {
        var page = Group(x =>
        {
            x.OrganizationId = "org-a";
            x.StartDate = Date(10);
            x.EndDate = Date(20);
        });
        var other = PublishedGroup(x =>
        {
            x.OrganizationId = "org-a";
            changeOther(x);
        });

        Assert.True(PageBuilderPermalinkConflicts.GetConflict(page, other, Now) == PageBuilderPermalinkConflict.Certain, difference);
    }

    [Theory]
    // Equally specific pages for the same audience.
    [InlineData(true, "", true, "", PageBuilderPermalinkConflict.Certain)]
    [InlineData(false, "", false, "", PageBuilderPermalinkConflict.Certain)]
    [InlineData(false, "vip, gold", false, "Gold", PageBuilderPermalinkConflict.Certain)]
    // Only a visitor in both groups gets both pages.
    [InlineData(false, "vip", false, "gold", PageBuilderPermalinkConflict.Possible)]
    [InlineData(true, "vip", true, "gold", PageBuilderPermalinkConflict.Possible)]
    // The more specific page wins for its audience: a page for user groups over one for everyone, a page for
    // registered users over a public one.
    [InlineData(false, "", false, "gold", PageBuilderPermalinkConflict.None)]
    [InlineData(true, "", true, "gold", PageBuilderPermalinkConflict.None)]
    [InlineData(true, "", false, "", PageBuilderPermalinkConflict.None)]
    [InlineData(true, "vip", false, "vip", PageBuilderPermalinkConflict.None)]
    public void GetConflict_Audience(bool visibility, string userGroups, bool otherVisibility, string otherUserGroups, PageBuilderPermalinkConflict expected)
    {
        var page = Group(x =>
        {
            x.Visibility = visibility;
            x.UserGroups = userGroups;
        });
        var other = PublishedGroup(x =>
        {
            x.Visibility = otherVisibility;
            x.UserGroups = otherUserGroups;
        });

        Assert.Equal(expected, PageBuilderPermalinkConflicts.GetConflict(page, other));
    }

    [Fact]
    public void ApplyPermalinkFilter_MatchesIgnoringCaseAndLeadingSlash()
    {
        var entities = new List<GroupedPageBuilderPageEntity>
        {
            new() { Id = "slash", Permalink = "/about" },
            new() { Id = "no-slash", Permalink = "about" },
            new() { Id = "case", Permalink = "/About" },
            new() { Id = "other", Permalink = "/about-us" },
            new() { Id = "empty", Permalink = null },
        };

        var result = GroupedPageSearchService.ApplyPermalinkFilter(entities.AsQueryable(), "About").Select(x => x.Id);

        Assert.Equal(["slash", "no-slash", "case"], result);
    }

    [Fact]
    public async Task FindPermalinkUsages_ReturnsOtherNotArchivedPagesWithConflicts()
    {
        var service = CreateService(
            Group(x => x.Id = "self"),
            PublishedGroup(x => x.Id = "published"),
            Group(x => x.Id = "draft"),
            Group(x =>
            {
                x.Id = "archived";
                x.Pages = [new PageBuilderPage { Id = "archived-page", Status = Archived }];
            }),
            PublishedGroup(x =>
            {
                x.Id = "other-permalink";
                x.Permalink = "/contacts";
            }));

        var usages = await new SeededGroupedPageSearchService(service).FindPermalinkUsagesAsync(Group(x => x.Id = "self"));

        Assert.Equal(["published", "draft"], usages.Select(x => x.GroupId));
        Assert.Equal(PageBuilderPermalinkConflict.Certain, usages[0].Conflict);
        Assert.Equal(PageBuilderPermalinkConflict.None, usages[1].Conflict);
    }

    [Fact]
    public async Task PublishGroup_CertainConflict_ReturnsConflictAndChangesNothing()
    {
        var service = CreateService(Group(x => x.Id = "new"), PublishedGroup(x => x.Id = "old"));
        var controller = CreateController(service);

        var result = await controller.PublishGroup("new", publish: true, cancellationToken: TestContext.Current.CancellationToken);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(Draft, (await service.GetByIdAsync("new")).Status);
        Assert.Equal(Published, (await service.GetByIdAsync("old")).Status);
    }

    [Fact]
    public async Task PublishGroup_UnpublishConflicting_PublishesAndUnpublishesTheOtherPage()
    {
        var service = CreateService(Group(x => x.Id = "new"), PublishedGroup(x => x.Id = "old"));
        var controller = CreateController(service);

        var result = await controller.PublishGroup("new", publish: true, unpublishConflicting: true, TestContext.Current.CancellationToken);

        Assert.IsType<OkResult>(result);
        Assert.Equal(Published, (await service.GetByIdAsync("new")).Status);
        var old = await service.GetByIdAsync("old");
        Assert.Equal(Draft, old.Status);
        Assert.Single(old.Pages);
    }

    [Fact]
    public async Task PublishGroup_UnpublishConflictingWithDraft_ReturnsConflictAndChangesNothing()
    {
        var service = CreateService(
            Group(x => x.Id = "new"),
            Group(x =>
            {
                x.Id = "old";
                x.Pages =
                [
                    new PageBuilderPage { Id = "old-published", Status = Published },
                    new PageBuilderPage { Id = "old-draft", Status = Draft },
                ];
            }));
        var controller = CreateController(service);

        var result = await controller.PublishGroup("new", publish: true, unpublishConflicting: true, TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(Draft, (await service.GetByIdAsync("new")).Status);
        Assert.Contains((await service.GetByIdAsync("old")).Pages, x => x.Status == Published);
    }

    [Fact]
    public async Task PublishGroup_OtherPageChangedAfterTheSearch_IsNotUnpublished()
    {
        var service = CreateService(
            Group(x => x.Id = "new"),
            PublishedGroup(x =>
            {
                x.Id = "old";
                x.Permalink = "/about-us";
            }));
        var staleSearchResult = PublishedGroup(x => x.Id = "old");
        var controller = CreateController(service, new FixedGroupedPageSearchService([staleSearchResult]));

        var result = await controller.PublishGroup("new", publish: true, unpublishConflicting: true, TestContext.Current.CancellationToken);

        Assert.IsType<OkResult>(result);
        Assert.Equal(Published, (await service.GetByIdAsync("new")).Status);
        Assert.Equal(Published, (await service.GetByIdAsync("old")).Status);
    }

    [Fact]
    public async Task PublishGroup_OtherPageGotDraftAfterTheSearch_ReturnsConflictAndChangesNothing()
    {
        var service = CreateService(
            Group(x => x.Id = "new"),
            Group(x =>
            {
                x.Id = "old";
                x.Pages =
                [
                    new PageBuilderPage { Id = "old-published", Status = Published },
                    new PageBuilderPage { Id = "old-draft", Status = Draft },
                ];
            }));
        var staleSearchResult = PublishedGroup(x => x.Id = "old");
        var controller = CreateController(service, new FixedGroupedPageSearchService([staleSearchResult]));

        var result = await controller.PublishGroup("new", publish: true, unpublishConflicting: true, TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(Draft, (await service.GetByIdAsync("new")).Status);
        Assert.Contains((await service.GetByIdAsync("old")).Pages, x => x.Status == Published);
    }

    [Fact]
    public async Task PublishGroup_OtherPageUnpublishedAfterTheSearch_PublishesAndLeavesItAlone()
    {
        var service = CreateService(Group(x => x.Id = "new"), Group(x => x.Id = "old"));
        var staleSearchResult = PublishedGroup(x => x.Id = "old");
        var controller = CreateController(service, new FixedGroupedPageSearchService([staleSearchResult]));

        var result = await controller.PublishGroup("new", publish: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<OkResult>(result);
        Assert.Equal(Published, (await service.GetByIdAsync("new")).Status);
        Assert.Equal(Draft, (await service.GetByIdAsync("old")).Status);
    }

    [Fact]
    public async Task PublishGroup_PossibleConflict_Publishes()
    {
        var service = CreateService(
            Group(x =>
            {
                x.Id = "new";
                x.UserGroups = "vip";
            }),
            PublishedGroup(x =>
            {
                x.Id = "old";
                x.UserGroups = "gold";
            }));
        var controller = CreateController(service);

        var result = await controller.PublishGroup("new", publish: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<OkResult>(result);
        Assert.Equal(Published, (await service.GetByIdAsync("new")).Status);
        Assert.Equal(Published, (await service.GetByIdAsync("old")).Status);
    }

    [Fact]
    public async Task GetPermalinkUsages_ChecksThePageAsSent()
    {
        var service = CreateService(PublishedGroup(x =>
        {
            x.Id = "old";
            x.OrganizationId = "org-a";
        }));
        var controller = CreateController(service);

        var result = await controller.GetPermalinkUsages(Group(x =>
        {
            x.Id = "new";
            x.OrganizationId = "org-b";
        }));

        var usage = Assert.Single(Assert.IsAssignableFrom<IList<PageBuilderPermalinkUsage>>(Assert.IsType<OkObjectResult>(result.Result).Value));
        Assert.Equal("old", usage.GroupId);
        Assert.Equal(PageBuilderPermalinkConflict.None, usage.Conflict);
        Assert.True(usage.CanUnpublish);
    }

    private static GroupedPageBuilderPage Group(Action<GroupedPageBuilderPage> change = null)
    {
        var group = new GroupedPageBuilderPage
        {
            Id = "group",
            StoreId = StoreId,
            Name = "Page",
            Permalink = "/about",
            CultureName = "en-US",
            Visibility = false,
        };
        change?.Invoke(group);
        group.Pages = group.Pages.Count > 0 ? group.Pages : [new PageBuilderPage { Id = $"{group.Id}-draft", Status = Draft }];
        return group;
    }

    private static GroupedPageBuilderPage PublishedGroup(Action<GroupedPageBuilderPage> change = null)
    {
        return Group(x =>
        {
            change?.Invoke(x);
            x.Pages = x.Pages.Count > 0 ? x.Pages : [new PageBuilderPage { Id = $"{x.Id}-published", Status = Published }];
        });
    }

    private static DateTime Date(int day) => new(2026, 1, day, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime Now = new(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PublishedRenameContentPreservationTests.FakeGroupedPageService CreateService(params GroupedPageBuilderPage[] groups)
    {
        var service = new PublishedRenameContentPreservationTests.FakeGroupedPageService();
        foreach (var group in groups)
        {
            foreach (var page in group.Pages)
            {
                page.StoreId = group.StoreId;
                page.GroupId = group.Id;
            }
            service.SeedGroup(group);
        }
        return service;
    }

    private static PageBuilderPageController CreateController(
        PublishedRenameContentPreservationTests.FakeGroupedPageService service,
        IGroupedPageSearchService searchService = null)
    {
        var pageService = new PublishedRenameContentPreservationTests.FakePageBuilderPageService(service);
        var pageContentService = new PageBuilderPageContentService(
            pageService,
            service,
            new NoopSharedComponentReferenceIndexService(),
            new PublishedRenameContentPreservationTests.NoopEventPublisher(),
            NullLogger<PageBuilderPageContentService>.Instance);
        return new PageBuilderPageController(
            pageService,
            service,
            searchService ?? new SeededGroupedPageSearchService(service),
            new PublishedRenameContentPreservationTests.AllowAllAuthorizationService(),
            new PublishedRenameContentPreservationTests.NoopPageDocumentSearchService(),
            pageContentService,
            NullLogger<PageBuilderPageController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
            },
        };
    }

    // Returns every seeded group of the store; FindPermalinkUsagesAsync compares the permalinks itself.
    private sealed class SeededGroupedPageSearchService(PublishedRenameContentPreservationTests.FakeGroupedPageService service)
        : IGroupedPageSearchService
    {
        public async Task<GroupedPageBuilderPageSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria, bool clone = true)
        {
            var groups = (await service.GetAsync(SeededIds))
                .Where(x => x.StoreId == criteria.StoreId)
                .ToList();
            return new GroupedPageBuilderPageSearchResult
            {
                TotalCount = groups.Count,
                Results = groups.Skip(criteria.Skip).Take(criteria.Take).ToList(),
            };
        }

        private static readonly string[] SeededIds = ["self", "new", "old", "published", "draft", "archived", "other-permalink"];
    }

    private sealed class FixedGroupedPageSearchService(IList<GroupedPageBuilderPage> groups) : IGroupedPageSearchService
    {
        public Task<GroupedPageBuilderPageSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria, bool clone = true)
        {
            return Task.FromResult(new GroupedPageBuilderPageSearchResult
            {
                TotalCount = groups.Count,
                Results = groups.Skip(criteria.Skip).Take(criteria.Take).ToList(),
            });
        }
    }

    private const string StoreId = "store";
}
