using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.ExportImport;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

// Grouped saves expire the static grouped-page cache regions; running in parallel with tests that assert
// on those regions (e.g. GroupedPageStoreMoveConcurrencyTests) makes them flaky.
[Collection(nameof(MultilanguagePageServiceTests))]
public class MultilanguagePageServiceTests
{
    private const string StoreId = "store";

    [Fact]
    public async Task SaveChangesAsync_NewRoot_TakesDefaultCultureFromStore()
    {
        await using var fixture = await Fixture.CreateAsync();

        var root = await fixture.CreateRootAsync();

        Assert.Equal("en-US", root.DefaultCultureName);
    }

    [Fact]
    public async Task SaveChangesAsync_RootChanged_PropagatesToInheritedFieldsOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US");
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", overriddenFields: "Permalink");

        root.Name = "About";
        root.Permalink = "/about";
        root.Visibility = false;
        await fixture.Roots.SaveChangesAsync([root]);

        var enEntity = await fixture.LoadGroupAsync(en.Id);
        var nlEntity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Equal(("About", "/about", false), (enEntity.Name, enEntity.Permalink, enEntity.Visibility));
        Assert.Equal(("About", "/over-ons", false), (nlEntity.Name, nlEntity.Permalink, nlEntity.Visibility));
    }

    [Fact]
    public async Task GroupSave_ClientUnawareOfMultilanguageFields_KeepsLinkAndMask()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", overriddenFields: "Permalink");

        var legacyPayload = await fixture.Groups.GetByIdAsync(nl.Id);
        legacyPayload.MultilanguagePageId = null;
        legacyPayload.OverriddenFields = null;
        await fixture.Groups.SaveChangesAsync([legacyPayload]);

        var entity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Equal(root.Id, entity.MultilanguagePageId);
        Assert.Equal("Permalink", entity.OverriddenFields);
    }

    [Fact]
    public async Task GroupSave_LanguageVersion_DerivesMaskFromRootAndKeepsSharedName()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL");

        var edited = await fixture.Groups.GetByIdAsync(nl.Id);
        edited.Permalink = "/over-ons";
        edited.Name = "Over ons";
        await fixture.Groups.SaveChangesAsync([edited]);

        var entity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Equal(("Permalink", "/over-ons", root.Name), (entity.OverriddenFields, entity.Permalink, entity.Name));

        var reverted = await fixture.Groups.GetByIdAsync(nl.Id);
        reverted.Permalink = root.Permalink;
        await fixture.Groups.SaveChangesAsync([reverted]);

        Assert.Equal(string.Empty, (await fixture.LoadGroupAsync(nl.Id)).OverriddenFields);
    }

    [Fact]
    public async Task ConvertAsync_LandingPage_BecomesDefaultLanguageVersionWithoutChanges()
    {
        await using var fixture = await Fixture.CreateAsync();
        var landing = await fixture.CreateLanguageAsync(null, "nl-NL", permalink: "/over-ons", name: "Over ons", draftContent: Content("nl"));

        var root = await fixture.Roots.ConvertAsync(landing.Id, TestContext.Current.CancellationToken);

        Assert.Equal(("Over ons", "/over-ons", "nl-NL"), (root.Name, root.Permalink, root.DefaultCultureName));
        var entity = await fixture.LoadGroupAsync(landing.Id);
        Assert.Equal((root.Id, string.Empty, "/over-ons", "Over ons"),
            (entity.MultilanguagePageId, entity.OverriddenFields, entity.Permalink, entity.Name));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.ConvertAsync(landing.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Outdated_SourcePublishedAfterTranslation_UntilTranslationRepublishedOrMarked()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        await fixture.Roots.PublishAllAsync(root.Id, TestContext.Current.CancellationToken);
        Assert.False(await IsOutdated(fixture, root, "nl-NL"));

        await Task.Delay(20, TestContext.Current.CancellationToken);
        await fixture.PublishNewDraftAsync(en.Id, Content("en-2"));
        Assert.True(await IsOutdated(fixture, root, "nl-NL"));
        Assert.False(await IsOutdated(fixture, root, "en-US"));

        await fixture.PublishNewDraftAsync(nl.Id, Content("nl-2"));
        Assert.False(await IsOutdated(fixture, root, "nl-NL"));

        await Task.Delay(20, TestContext.Current.CancellationToken);
        await fixture.PublishNewDraftAsync(en.Id, Content("en-3"));
        Assert.True(await IsOutdated(fixture, root, "nl-NL"));

        await fixture.Roots.MarkUpToDateAsync(nl.Id, TestContext.Current.CancellationToken);
        Assert.False(await IsOutdated(fixture, root, "nl-NL"));
    }

    [Fact]
    public async Task Outdated_CopyOfPublishedSource_IsUpToDate()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        await fixture.PublishNewDraftAsync(en.Id, null);

        await fixture.Roots.CreateLanguageAsync(root.Id, "nl-NL", copyContent: true, TestContext.Current.CancellationToken);

        Assert.False(await IsOutdated(fixture, root, "nl-NL"));
    }

    private static async Task<bool> IsOutdated(Fixture fixture, MultilanguagePage root, string cultureName)
    {
        var languages = await fixture.Roots.GetLanguagesAsync(root.Id, TestContext.Current.CancellationToken);
        return languages.Single(x => x.CultureName == cultureName).IsOutdated;
    }

    [Fact]
    public async Task ResetOverridesAsync_AppliesRootValueToOverriddenLanguages_KeepsOtherOverrides()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US");
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons");
        var edited = await fixture.Groups.GetByIdAsync(nl.Id);
        edited.Visibility = false;
        await fixture.Groups.SaveChangesAsync([edited]);

        root.Permalink = "/about";
        await fixture.Roots.SaveChangesAsync([root]);
        Assert.Equal("Permalink,Visibility", (await fixture.LoadGroupAsync(nl.Id)).OverriddenFields);

        var changed = await fixture.Roots.ResetOverridesAsync(root.Id, ["permalink"], TestContext.Current.CancellationToken);

        Assert.Equal(["nl-NL"], changed);
        var entity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Equal(("/about", "Visibility", false), (entity.Permalink, entity.OverriddenFields, entity.Visibility));
        Assert.Equal("/about", (await fixture.LoadGroupAsync(en.Id)).Permalink);
    }

    [Fact]
    public async Task List_MissingTranslation_ListsPagesWithoutAVersionInAStoreOrRequestedLanguage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var complete = await fixture.CreateRootAsync("Complete");
        foreach (var culture in new[] { "en-US", "nl-NL", "de-DE" })
        {
            await fixture.CreateLanguageAsync(complete, culture, draftContent: Content(culture));
        }
        var englishDutch = await fixture.CreateRootAsync("English and Dutch");
        await fixture.CreateLanguageAsync(englishDutch, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(englishDutch, "nl-NL", draftContent: Content("nl"));
        var english = await fixture.CreateRootAsync("English");
        await fixture.CreateLanguageAsync(english, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(null, "en-US", permalink: "/landing", name: "Landing", draftContent: Content("landing"));

        var anyLanguage = await ListIds(fixture, new PageBuilderPageSearchCriteria { TranslationStatus = "Missing" });
        var dutch = await ListIds(fixture, new PageBuilderPageSearchCriteria { TranslationStatus = "Missing", LanguageCode = "nl-NL" });

        Assert.Equal(new[] { englishDutch.Id, english.Id }.Order(), anyLanguage.Order());
        Assert.Equal([english.Id], dutch);
    }

    [Fact]
    public async Task List_OutdatedTranslation_ListsPagesWithATranslationBehindTheSource()
    {
        await using var fixture = await Fixture.CreateAsync();
        var outdated = await fixture.CreateRootAsync("Outdated");
        var outdatedEn = await fixture.CreateLanguageAsync(outdated, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(outdated, "nl-NL", draftContent: Content("nl"));
        await fixture.Roots.PublishAllAsync(outdated.Id, TestContext.Current.CancellationToken);
        var upToDate = await fixture.CreateRootAsync("Up to date");
        await fixture.CreateLanguageAsync(upToDate, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(upToDate, "nl-NL", draftContent: Content("nl"));
        await fixture.Roots.PublishAllAsync(upToDate.Id, TestContext.Current.CancellationToken);

        await Task.Delay(20, TestContext.Current.CancellationToken);
        await fixture.PublishNewDraftAsync(outdatedEn.Id, Content("en-2"));

        Assert.Equal([outdated.Id], await ListIds(fixture, new PageBuilderPageSearchCriteria { TranslationStatus = "Outdated" }));
        Assert.Equal([outdated.Id], await ListIds(fixture, new PageBuilderPageSearchCriteria { TranslationStatus = "Outdated", LanguageCode = "nl-NL" }));
        Assert.Empty(await ListIds(fixture, new PageBuilderPageSearchCriteria { TranslationStatus = "Outdated", LanguageCode = "de-DE" }));
    }

    [Fact]
    public async Task List_OutdatedTranslation_IgnoresArchivedTranslations()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        await fixture.Roots.PublishAllAsync(root.Id, TestContext.Current.CancellationToken);
        var archived = await fixture.Groups.GetByIdAsync(nl.Id);
        Assert.True(GroupedPagePublishing.Archive(archived));
        await fixture.Groups.SaveChangesAsync([archived]);

        await Task.Delay(20, TestContext.Current.CancellationToken);
        await fixture.PublishNewDraftAsync(en.Id, Content("en-2"));

        Assert.Empty(await ListIds(fixture, new PageBuilderPageSearchCriteria { TranslationStatus = "Outdated" }));
    }

    private static async Task<IList<string>> ListIds(Fixture fixture, PageBuilderPageSearchCriteria criteria)
    {
        criteria.StoreId = StoreId;
        criteria.Take = 20;
        var result = await fixture.List.SearchAsync(criteria, TestContext.Current.CancellationToken);
        return result.Results.Select(x => x.Id).ToList();
    }

    [Fact]
    public async Task CloneAsync_CopiesEveryLanguageAsDraftWithCopyPermalinks()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("hero-en"));
        await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", draftContent: Content("hero-nl"));
        await fixture.Roots.PublishAllAsync(root.Id, TestContext.Current.CancellationToken);

        var clone = await fixture.Roots.CloneAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.NotEqual(root.Id, clone.Id);
        Assert.Equal(("About us (copy)", "/about-us-copy"), (clone.Name, clone.Permalink));
        var languages = await fixture.Roots.GetLanguagesAsync(clone.Id, TestContext.Current.CancellationToken);
        var versions = languages.Where(x => x.Page != null).ToDictionary(x => x.CultureName, x => x.Page);
        Assert.Equal(("/about-us-copy", string.Empty, "Draft"), (versions["en-US"].Permalink, versions["en-US"].OverriddenFields, versions["en-US"].Status));
        Assert.Equal(("/over-ons-copy", "Permalink", "Draft"), (versions["nl-NL"].Permalink, versions["nl-NL"].OverriddenFields, versions["nl-NL"].Status));
        var content = (await fixture.Groups.LoadGroupContentAsync(versions["en-US"], draft: true, TestContext.Current.CancellationToken)).Content;
        Assert.Contains("hero-en", content);
        // The original keeps its addresses and published versions.
        Assert.Equal(("/about-us", "Published"), ((await fixture.LoadGroupAsync(en.Id)).Permalink, (await fixture.Groups.GetByIdAsync(en.Id)).Status));
    }

    [Fact]
    public async Task FindPermalinkUsageAsync_ReportsOtherActivePagesInTheSameLanguage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var ownEn = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var otherEn = await fixture.CreateLanguageAsync(null, "en-US", permalink: "about-us", name: "Other", draftContent: Content("other"));
        await fixture.CreateLanguageAsync(null, "de-DE", permalink: "/about-us", name: "German", draftContent: Content("de"));
        var archived = await fixture.CreateLanguageAsync(null, "en-US", permalink: "/about-us", name: "Archived", draftContent: Content("old"));
        var archivedGroup = await fixture.Groups.GetByIdAsync(archived.Id);
        GroupedPagePublishing.Archive(archivedGroup);
        await fixture.Groups.SaveChangesAsync([archivedGroup]);

        var usage = await fixture.List.FindPermalinkUsageAsync(new PermalinkUsageRequest
        {
            StoreId = StoreId,
            Permalink = "/about-us",
            CultureNames = ["en-US"],
            ExcludeMultilanguagePageId = root.Id,
        }, TestContext.Current.CancellationToken);

        // Its own versions, other languages and archived pages do not count; a permalink without the slash does.
        Assert.Equal([otherEn.Id], usage.Select(x => x.GroupId));
        Assert.NotNull(ownEn);
    }

    [Fact]
    public async Task LinkAsync_ExistingLandingPage_KeepsItsDifferingValuesAsOverrides()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var landing = await fixture.CreateLanguageAsync(null, "nl-NL", permalink: "/veelgestelde-vragen", name: "Veelgestelde vragen");

        await fixture.Roots.LinkAsync(root.Id, landing.Id, TestContext.Current.CancellationToken);

        var entity = await fixture.LoadGroupAsync(landing.Id);
        Assert.Equal(root.Id, entity.MultilanguagePageId);
        // The landing page was private and had its own permalink: both differ from the root and stay as they were.
        Assert.Equal("Permalink,Visibility", entity.OverriddenFields);
        Assert.Equal("/veelgestelde-vragen", entity.Permalink);
        Assert.False(entity.Visibility);
        Assert.Equal(root.Name, entity.Name);
    }

    [Fact]
    public async Task LinkAsync_LanguageAlreadyPresent_Throws()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL");
        var second = await fixture.CreateLanguageAsync(null, "nl-NL", permalink: "/faq");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.LinkAsync(root.Id, second.Id, TestContext.Current.CancellationToken));

        Assert.Null((await fixture.LoadGroupAsync(second.Id)).MultilanguagePageId);
    }

    [Fact]
    public async Task UnlinkAsync_KeepsValuesAndClearsMask()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", overriddenFields: "Permalink");

        await fixture.Roots.UnlinkAsync(nl.Id, TestContext.Current.CancellationToken);

        var entity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Null(entity.MultilanguagePageId);
        Assert.Equal(string.Empty, entity.OverriddenFields);
        Assert.Equal("/over-ons", entity.Permalink);
    }

    [Fact]
    public async Task DeleteAsync_DetachesLanguagesAndRemovesRoot()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US");

        await fixture.Roots.DeleteAsync([root.Id]);

        await using var context = fixture.CreateContext();
        Assert.False(await context.Set<MultilanguagePageEntity>().AnyAsync(x => x.Id == root.Id, TestContext.Current.CancellationToken));
        var entity = await fixture.LoadGroupAsync(en.Id);
        Assert.Null(entity.MultilanguagePageId);
        Assert.Equal(root.Permalink, entity.Permalink);
    }

    [Fact]
    public async Task GetLanguagesAsync_ListsStoreLanguagesDefaultFirst_ThenRemovedLanguages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        await fixture.CreateLanguageAsync(root, "fr-FR", permalink: "/a-propos", overriddenFields: "Permalink", draftContent: Content("fr"));

        var languages = await fixture.Roots.GetLanguagesAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("en-US", true, true, MultilanguagePageLanguage.Missing),
                ("nl-NL", false, true, "Draft"),
                ("de-DE", false, true, MultilanguagePageLanguage.Missing),
                ("fr-FR", false, false, "Draft"),
            ],
            languages.Select(x => (x.CultureName, x.IsDefault, x.IsStoreLanguage, x.Status)));
        Assert.Null(languages[0].Page);
        Assert.NotNull(languages[1].Page);
    }

    [Fact]
    public async Task GetLanguagesAsync_ReportsEachVersionStatus()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        var de = await fixture.CreateLanguageAsync(root, "de-DE", draftContent: Content("de"));
        await fixture.Roots.PublishAllAsync(root.Id, TestContext.Current.CancellationToken);
        var archived = await fixture.Groups.GetByIdAsync(de.Id);
        Assert.True(GroupedPagePublishing.Archive(archived));
        await fixture.Groups.SaveChangesAsync([archived]);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await fixture.PublishNewDraftAsync(en.Id, Content("en-2"));

        var languages = await fixture.Roots.GetLanguagesAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            [("en-US", "Published", false), ("nl-NL", "Published", true), ("de-DE", "Archived", false)],
            languages.Select(x => (x.CultureName, x.Status, x.IsOutdated)));
        Assert.NotNull(nl);
    }

    [Fact]
    public async Task CreateLanguageAsync_CopyContent_StartsFromDefaultLanguageWithOwnSettings()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("hero-en"));

        var nl = await fixture.Roots.CreateLanguageAsync(root.Id, "nl-NL", copyContent: true, TestContext.Current.CancellationToken);

        var content = (await fixture.Groups.LoadGroupContentAsync(nl, draft: true, TestContext.Current.CancellationToken)).Content;
        Assert.Contains("hero-en", content);
        Assert.Contains("\"cultureName\":\"nl-NL\"", content);
        var entity = await fixture.LoadGroupAsync(nl.Id);
        Assert.Equal((root.Id, root.Permalink, string.Empty), (entity.MultilanguagePageId, entity.Permalink, entity.OverriddenFields));
    }

    [Fact]
    public async Task CreateLanguageAsync_NotAStoreLanguageOrAlreadyPresent_Throws()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Roots.CreateLanguageAsync(root.Id, "fr-FR", copyContent: false, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Roots.CreateLanguageAsync(root.Id, "nl-NL", copyContent: false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAllAsync_PublishesDraftsAndSkipsVersionsWithoutDraft()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(root, "nl-NL");

        var result = await fixture.Roots.PublishAllAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(["en-US"], result.ProcessedCultureNames);
        Assert.Equal([("nl-NL", MultilanguagePageBatchResult.NoDraftReason)], result.Skipped.Select(x => (x.CultureName, x.Reason)));
        Assert.Equal("Published", (await fixture.Groups.GetByIdAsync(en.Id)).Status);
    }

    [Fact]
    public async Task ArchiveAllAsync_ArchivesEveryVersion()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));

        var result = await fixture.Roots.ArchiveAllAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(["en-US", "nl-NL"], result.ProcessedCultureNames.Order());
        Assert.Equal("Archived", (await fixture.Groups.GetByIdAsync(en.Id)).Status);
        Assert.Equal("Archived", (await fixture.Groups.GetByIdAsync(nl.Id)).Status);
    }

    [Fact]
    public void ToPageDocument_LanguageVersion_CarriesTranslationGroup()
    {
        var group = new GroupedPageBuilderPage { Id = "group", CultureName = "nl-NL", MultilanguagePageId = "root" };
        var page = new PageBuilderPage { Id = "page", GroupId = "group" };

        var document = page.ToPageDocument(group, Content("nl"));

        Assert.Equal("root", document.TranslationGroupId);
    }

    [Fact]
    public async Task List_MultilanguagePageIsOneRowWithLanguages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        var landing = await fixture.CreateLanguageAsync(null, "en-US", permalink: "/summer", name: "Summer", draftContent: Content("summer"));

        var result = await fixture.List.SearchAsync(new PageBuilderPageSearchCriteria { StoreId = StoreId, Take = 10 }, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.TotalCount);
        var multilanguage = result.Results.Single(x => x.Type == "Multilanguage");
        Assert.Equal(root.Id, multilanguage.Id);
        Assert.Equal(
            [("en-US", "Draft"), ("nl-NL", "Draft"), ("de-DE", MultilanguagePageLanguage.Missing)],
            multilanguage.Languages.Select(x => (x.CultureName, x.Status)));
        var landingRow = result.Results.Single(x => x.Type == "Landing");
        Assert.Equal(landing.Id, landingRow.Page.Id);
        Assert.Null(landingRow.Languages);
    }

    [Fact]
    public async Task List_VersionFilter_ListsRootWhenAnyLanguageMatches()
    {
        await using var fixture = await Fixture.CreateAsync();
        var withDraft = await fixture.CreateRootAsync("With draft");
        await fixture.CreateLanguageAsync(withDraft, "nl-NL", draftContent: Content("nl"));
        var archived = await fixture.CreateRootAsync("Archived");
        var archivedEn = await fixture.CreateLanguageAsync(archived, "en-US", draftContent: Content("en"));
        await fixture.Roots.ArchiveAllAsync(archived.Id, TestContext.Current.CancellationToken);
        await fixture.CreateRootAsync("No languages");

        var result = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Lifecycle = "drafts", Take = 10 },
            TestContext.Current.CancellationToken);

        Assert.Equal([withDraft.Id], result.Results.Select(x => x.Id));
        Assert.NotNull(archivedEn);
    }

    [Fact]
    public async Task List_NoVersionFilter_ListsRootWithoutLanguages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync("No languages");

        var result = await fixture.List.SearchAsync(new PageBuilderPageSearchCriteria { StoreId = StoreId, Take = 10 }, TestContext.Current.CancellationToken);

        Assert.Equal([root.Id], result.Results.Select(x => x.Id));
    }

    [Fact]
    public async Task List_SortsAndPagesAcrossBothTypes_IgnoresUnknownSortColumn()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreateRootAsync("B root");
        await fixture.CreateLanguageAsync(null, "en-US", permalink: "/a", name: "A landing", draftContent: Content("a"));
        await fixture.CreateLanguageAsync(null, "en-US", permalink: "/c", name: "C landing", draftContent: Content("c"));

        var result = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Sort = "name;status", Skip = 1, Take = 2 },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(["B root", "C landing"], result.Results.Select(x => x.Name));
    }

    [Fact]
    public async Task List_PageTypeFilter()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(null, "en-US", permalink: "/a", name: "A landing", draftContent: Content("a"));

        var result = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, PageType = "Multilanguage", Take = 10 },
            TestContext.Current.CancellationToken);

        Assert.Equal([root.Id], result.Results.Select(x => x.Id));
    }

    [Fact]
    public async Task Import_RestoresMultilanguagePagesAndLinks_RegardlessOfSectionOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        // Pages come first on purpose: language versions must still find their root.
        var payload = JsonConvert.SerializeObject(new
        {
            PageBuilderPages = new[]
            {
                ExportedPage("g-en", "en-US", "/faq", "root-1"),
                ExportedPage("g-fr", "fr-FR", "/questions", "root-1"),
                ExportedPage("g-orphan", "de-DE", "/faq", "missing-root"),
            },
            PageBuilderMultilanguagePages = new[]
            {
                new MultilanguagePage { Id = "root-1", StoreId = StoreId, Name = "FAQ", Permalink = "/faq", DefaultCultureName = "en-US", Visibility = true },
            },
        });

        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        await fixture.CreateExportImport().DoImportAsync(input, _ => { }, TestContext.Current.CancellationToken);

        Assert.NotNull(await fixture.Roots.GetByIdAsync("root-1"));
        var en = await fixture.LoadGroupAsync("g-en");
        var fr = await fixture.LoadGroupAsync("g-fr");
        Assert.Equal(("root-1", string.Empty, "FAQ"), (en.MultilanguagePageId, en.OverriddenFields, en.Name));
        Assert.Equal(("root-1", "Permalink", "FAQ"), (fr.MultilanguagePageId, fr.OverriddenFields, fr.Name));
        // A version whose root is not in the target becomes a Landing Page instead of failing the import.
        Assert.Null((await fixture.LoadGroupAsync("g-orphan")).MultilanguagePageId);
    }

    [Fact]
    public async Task Import_ExistingPage_FollowsExportedLink()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var landing = await fixture.CreateLanguageAsync(null, "nl-NL", permalink: "/over-ons", draftContent: Content("nl"));
        var exported = ExportedPage(landing.Id, "nl-NL", "/over-ons", root.Id);
        exported.Variants = [.. landing.Pages.Select(x => new PageBuilderExportPageVariant { PageId = x.Id, Status = x.Status })];
        var payload = JsonConvert.SerializeObject(new { PageBuilderPages = new[] { exported } });

        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var progress = new List<ExportImportProgressInfo>();
        await fixture.CreateExportImport().DoImportAsync(input, progress.Add, TestContext.Current.CancellationToken);

        var entity = await fixture.LoadGroupAsync(landing.Id);
        Assert.Equal((root.Id, "Permalink"), (entity.MultilanguagePageId, entity.OverriddenFields));
        Assert.All(progress, x => Assert.Empty(x.Errors));
    }

    private static PageBuilderExportPage ExportedPage(string groupId, string cultureName, string permalink, string multilanguagePageId)
    {
        return new PageBuilderExportPage
        {
            GroupId = groupId,
            Name = $"Page {cultureName}",
            Permalink = permalink,
            StoreId = StoreId,
            CultureName = cultureName,
            Visibility = true,
            MultilanguagePageId = multilanguagePageId,
            Variants = [new PageBuilderExportPageVariant { PageId = $"{groupId}-page", Status = "Draft" }],
        };
    }

    internal static string Content(string sectionType)
    {
        return $$"""{ "settings": {}, "content": [ { "type": "{{sectionType}}" } ] }""";
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _anchorConnection;
        private readonly string _connectionString;
        private readonly TestPlatformMemoryCache _cache = new();

        private Fixture(string connectionString, SqliteConnection anchorConnection)
        {
            _connectionString = connectionString;
            _anchorConnection = anchorConnection;

            Func<IPageBuilderModuleRepository> repositoryFactory = () => new PageBuilderModuleRepository(CreateContext());
            var events = new NoopEventPublisher();
            Groups = new GroupedPageService(
                repositoryFactory,
                () => throw new InvalidOperationException("Content repository is not used by these tests."),
                _cache,
                events,
                NullLogger<GroupedPageService>.Instance);
            var groupSearch = new GroupedPageSearchService(repositoryFactory, _cache, Groups, Options.Create(new CrudOptions()));
            GroupSearch = groupSearch;
            var storeService = StoreService = new FakeStoreService(new Store { Id = StoreId, DefaultLanguage = "en-US", Languages = ["en-US", "nl-NL", "de-DE"] });
            Roots = new MultilanguagePageService(
                repositoryFactory,
                _cache,
                events,
                Groups,
                Groups,
                groupSearch,
                storeService,
                new PageBuilderPageService(repositoryFactory, _cache, events));
            List = new PageBuilderPageListService(repositoryFactory, groupSearch, Groups, Roots, storeService);
        }

        public FakeStoreService StoreService { get; }

        public GroupedPageService Groups { get; }

        public MultilanguagePageService Roots { get; }

        public PageBuilderPageListService List { get; }

        public GroupedPageSearchService GroupSearch { get; }

        /// <summary>Publishes the current draft like the page blade does; with content, adds a new draft first.</summary>
        public async Task PublishNewDraftAsync(string groupId, string content)
        {
            var group = await Groups.GetByIdAsync(groupId);
            if (content != null)
            {
                group.Pages.Add(new PageBuilderPage { StoreId = StoreId, Status = "Draft", Content = content });
                await Groups.SaveChangesAsync([group]);
                group = await Groups.GetByIdAsync(groupId);
            }

            Assert.True(GroupedPagePublishing.TryPublishDraft(group, out _));
            await Groups.SaveChangesAsync([group]);
        }

        public PageBuilderExportImport CreateExportImport()
        {
            var jsonSerializer = JsonSerializer.CreateDefault();
            var sharedComponents = new PageBuilderSharedComponentExportImport(
                new NoopSharedComponentReferenceIndexService(), null!, null!, null!, jsonSerializer);

            return new PageBuilderExportImport(Groups, GroupSearch, null!, sharedComponents, null!, jsonSerializer, Roots, null);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"page-builder-multilanguage-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 10,
            }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync(TestContext.Current.CancellationToken);
            var fixture = new Fixture(connectionString, anchorConnection);

            await using var context = fixture.CreateContext();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return fixture;
        }

        public PageBuilderModuleDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>().UseSqlite(_connectionString).Options;
            return new PageBuilderModuleDbContext(options);
        }

        public async Task<MultilanguagePage> CreateRootAsync(string name = "About us")
        {
            var root = new MultilanguagePage
            {
                StoreId = StoreId,
                Name = name,
                Permalink = "/about-us",
                Visibility = true,
            };
            await Roots.SaveChangesAsync([root]);
            return root;
        }

        public async Task<GroupedPageBuilderPage> CreateLanguageAsync(
            MultilanguagePage root,
            string cultureName,
            string permalink = null,
            string name = null,
            string overriddenFields = null,
            string draftContent = null)
        {
            var group = new GroupedPageBuilderPage
            {
                StoreId = StoreId,
                CultureName = cultureName,
                MultilanguagePageId = root?.Id,
            };

            if (root != null)
            {
                MultilanguagePageInheritance.Apply(root, group);
            }

            group.Name = name ?? group.Name ?? "Landing";
            group.Permalink = permalink ?? group.Permalink;
            group.OverriddenFields = overriddenFields;

            if (draftContent != null)
            {
                group.Pages.Add(new PageBuilderPage { StoreId = StoreId, Status = "Draft", Content = draftContent });
            }

            await Groups.SaveChangesAsync([group]);
            return group;
        }

        public async Task<GroupedPageBuilderPageEntity> LoadGroupAsync(string id)
        {
            await using var context = CreateContext();
            return await context.Set<GroupedPageBuilderPageEntity>().AsNoTracking()
                .SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _cache.Dispose();
            await _anchorConnection.DisposeAsync();
        }
    }

    private sealed class NoopEventPublisher : IEventPublisher
    {
        public Task Publish<T>(T @event, System.Threading.CancellationToken cancellationToken = default)
            where T : IEvent
        {
            return Task.CompletedTask;
        }
    }
}

[CollectionDefinition(nameof(MultilanguagePageServiceTests), DisableParallelization = true)]
public class MultilanguagePageServiceTestsCollection;
