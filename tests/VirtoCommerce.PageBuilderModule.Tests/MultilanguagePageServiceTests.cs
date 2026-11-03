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
    public async Task CloneAsync_Twice_SecondCopyTakesTheNextFreeSuffixInEveryLanguage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", overriddenFields: "Permalink", draftContent: Content("nl"));

        var first = await fixture.Roots.CloneAsync(root.Id, TestContext.Current.CancellationToken);
        var second = await fixture.Roots.CloneAsync(root.Id, TestContext.Current.CancellationToken);
        // A copy of a copy counts on from the original.
        var third = await fixture.Roots.CloneAsync(first.Id, TestContext.Current.CancellationToken);

        Assert.Equal(("About us (copy)", "/about-us-copy"), (first.Name, first.Permalink));
        Assert.Equal(("About us (copy 2)", "/about-us-copy-2"), (second.Name, second.Permalink));
        Assert.Equal(("About us (copy 3)", "/about-us-copy-3"), (third.Name, third.Permalink));
        var languages = await fixture.Roots.GetLanguagesAsync(second.Id, TestContext.Current.CancellationToken);
        Assert.Equal(["/about-us-copy-2", "/over-ons-copy-2"], languages.Where(x => x.Page != null).Select(x => x.Page.Permalink).Order());
    }

    [Fact]
    public async Task GetCopyNameAsync_LandingPage_TakesTheFirstSuffixNoPageOfTheStoreUses()
    {
        await using var fixture = await Fixture.CreateAsync();
        var landing = await fixture.CreateLanguageAsync(null, "en-US", permalink: "/summer", name: "Summer");
        // A draft copy takes the address too.
        await fixture.CreateLanguageAsync(null, "en-US", permalink: "/summer-copy", name: "Summer (copy)");

        var copy = await fixture.List.GetCopyNameAsync(landing, TestContext.Current.CancellationToken);

        Assert.Equal(("Summer (copy 2)", "/summer-copy-2"), (copy.Name, copy.Permalink));
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
    public async Task LinkAndConvert_LanguageInOtherCase_StoreTheStoreSpelling()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var linked = await fixture.CreateLanguageAsync(null, "NL-nl", permalink: "/veelgestelde-vragen");
        var converted = await fixture.CreateLanguageAsync(null, "DE-de", permalink: "/haeufige-fragen");

        await fixture.Roots.LinkAsync(root.Id, linked.Id, TestContext.Current.CancellationToken);
        var convertedRoot = await fixture.Roots.ConvertAsync(converted.Id, TestContext.Current.CancellationToken);

        Assert.Equal("nl-NL", (await fixture.LoadGroupAsync(linked.Id)).CultureName);
        Assert.Equal("de-DE", (await fixture.LoadGroupAsync(converted.Id)).CultureName);
        Assert.Equal("de-DE", convertedRoot.DefaultCultureName);
    }

    [Fact]
    public async Task SaveChangesAsync_RootWithAnotherStore_KeepsItsStore()
    {
        // An import file or a client must not move the root away from its language versions.
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        root.StoreId = "another-store";

        await fixture.Roots.SaveChangesAsync([root]);

        Assert.Equal(StoreId, (await fixture.Roots.GetNoCloneAsync(root.Id)).StoreId);
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
    public async Task LinkAsync_PageWithoutLanguage_ThrowsAndKeepsItUnlinked()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var noLanguage = await fixture.CreateLanguageAsync(null, null, permalink: "/faq");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.LinkAsync(root.Id, noLanguage.Id, TestContext.Current.CancellationToken));

        Assert.Null((await fixture.LoadGroupAsync(noLanguage.Id)).MultilanguagePageId);
    }

    [Fact]
    public async Task LinkAsync_PageInLanguageTheStoreDoesNotServe_ThrowsAndKeepsItUnlinked()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var unknownLanguage = await fixture.CreateLanguageAsync(null, "xx-XX", permalink: "/faq");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.LinkAsync(root.Id, unknownLanguage.Id, TestContext.Current.CancellationToken));

        Assert.Null((await fixture.LoadGroupAsync(unknownLanguage.Id)).MultilanguagePageId);
    }

    [Fact]
    public async Task GetLanguagesAsync_DefaultLanguageTheStoreDoesNotServe_IsNotAStoreLanguage()
    {
        await using var fixture = await Fixture.CreateAsync();
        // Stored before the check existed, or the language was removed from the store later.
        var root = new MultilanguagePage { StoreId = StoreId, Name = "FAQ", Permalink = "/faq", DefaultCultureName = "fr-FR" };
        await fixture.Roots.SaveChangesAsync([root]);

        var languages = await fixture.Roots.GetLanguagesAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(("fr-FR", true, false), (languages[0].CultureName, languages[0].IsDefault, languages[0].IsStoreLanguage));
        Assert.All(languages.Skip(1), x => Assert.True(x.IsStoreLanguage));
    }

    [Fact]
    public async Task MarkUpToDateAsync_SourceNotPublishedOrSourceItself_Throws()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.MarkUpToDateAsync(nl.Id, TestContext.Current.CancellationToken));

        await fixture.PublishNewDraftAsync(en.Id, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.MarkUpToDateAsync(en.Id, TestContext.Current.CancellationToken));
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
    public async Task PublishAllAsync_DraftPublishedWhileWaitingForTheLock_IsNotPublishedAgain()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));

        // A parallel publish-all holds the lock and publishes the source while this call waits.
        fixture.Lock.Hold();
        var waiting = fixture.Roots.PublishAllAsync(root.Id, TestContext.Current.CancellationToken);
        await fixture.Lock.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await fixture.PublishNewDraftAsync(en.Id, null);
        fixture.Lock.Release();
        var result = await waiting;

        // The versions are read under the lock, so the published source is not processed a second time.
        Assert.Equal(["nl-NL"], result.ProcessedCultureNames);
        Assert.Equal(["en-US"], result.Skipped.Where(x => x.Reason == MultilanguagePageBatchResult.NoDraftReason).Select(x => x.CultureName));
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
    public async Task ArchiveAllAsync_ThenRootRenamed_VersionsStayArchived()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        await fixture.Roots.ArchiveAllAsync(root.Id, TestContext.Current.CancellationToken);

        root.Name = "About";
        root.Permalink = "/about";
        await fixture.Roots.SaveChangesAsync([root]);
        await fixture.Roots.ResetOverridesAsync(root.Id, ["permalink"], TestContext.Current.CancellationToken);

        foreach (var id in new[] { en.Id, nl.Id })
        {
            var group = await fixture.Groups.GetByIdAsync(id);
            Assert.Equal("Archived", group.Status);
            Assert.All(group.Pages, x => Assert.Equal("Archived", x.Status));
            Assert.Equal(("About", "/about"), (group.Name, group.Permalink));
        }
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
        var noLanguages = await fixture.CreateRootAsync("No languages");

        var drafts = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Lifecycle = "drafts", Take = 10 },
            TestContext.Current.CancellationToken);
        var active = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Lifecycle = "active", Take = 10 },
            TestContext.Current.CancellationToken);
        var draftsInLanguage = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Lifecycle = "drafts", LanguageCode = "nl-NL", Take = 10 },
            TestContext.Current.CancellationToken);

        // A root without language versions is still being authored, so only the drafts list shows it.
        Assert.Equal(new[] { noLanguages.Id, withDraft.Id }.Order(), drafts.Results.Select(x => x.Id).Order());
        Assert.Empty(active.Results);
        Assert.Equal([withDraft.Id], draftsInLanguage.Results.Select(x => x.Id));
        Assert.NotNull(archivedEn);
    }

    [Fact]
    public async Task List_DraftsWithKeyword_ListsRootWithoutLanguagesByItsNameOrPermalink()
    {
        await using var fixture = await Fixture.CreateAsync();
        var faq = await fixture.CreateRootAsync("FAQ");
        await fixture.CreateRootAsync("Contacts");

        var result = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Lifecycle = "drafts", Keyword = "faq", Take = 10 },
            TestContext.Current.CancellationToken);

        Assert.Equal([faq.Id], result.Results.Select(x => x.Id));
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

    [Fact]
    public async Task GroupSave_LanguageVersionMovedToTakenLanguageOrAnotherStore_Throws()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL");
        var de = await fixture.CreateLanguageAsync(root, "de-DE");

        var takenLanguage = await fixture.Groups.GetByIdAsync(de.Id);
        takenLanguage.CultureName = "nl-NL";
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Groups.SaveChangesAsync([takenLanguage]));

        var anotherStore = await fixture.Groups.GetByIdAsync(de.Id);
        anotherStore.StoreId = "another-store";
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Groups.SaveChangesAsync([anotherStore]));

        var entity = await fixture.LoadGroupAsync(de.Id);
        Assert.Equal(("de-DE", StoreId), (entity.CultureName, entity.StoreId));
    }

    [Fact]
    public async Task GroupSave_NewPageLinkedToRootOfAnotherStoreOrTakenLanguage_Throws()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL");
        var foreignRoot = new MultilanguagePage { StoreId = "another-store", Name = "Foreign", Permalink = "/foreign", DefaultCultureName = "en-US" };
        await fixture.Roots.SaveChangesAsync([foreignRoot]);

        var foreign = new GroupedPageBuilderPage { StoreId = StoreId, CultureName = "en-US", Name = "Page", MultilanguagePageId = foreignRoot.Id };
        var duplicate = new GroupedPageBuilderPage { StoreId = StoreId, CultureName = "nl-NL", Name = "Page", MultilanguagePageId = root.Id };

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Groups.SaveChangesAsync([foreign]));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Groups.SaveChangesAsync([duplicate]));

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Set<GroupedPageBuilderPageEntity>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Import_FileWithoutMultilanguagePages_KeepsExistingLinks()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", permalink: "/over-ons", overriddenFields: "Permalink", draftContent: Content("nl"));
        // A file exported before multilanguage pages existed: no roots section and no link on the page.
        var exported = ExportedPage(nl.Id, "nl-NL", "/over-ons", multilanguagePageId: null);
        exported.Variants = [.. nl.Pages.Select(x => new PageBuilderExportPageVariant { PageId = x.Id, Status = x.Status })];
        var payload = JsonConvert.SerializeObject(new { PageBuilderPages = new[] { exported } });

        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        await fixture.CreateExportImport().DoImportAsync(input, _ => { }, TestContext.Current.CancellationToken);

        Assert.Equal(root.Id, (await fixture.LoadGroupAsync(nl.Id)).MultilanguagePageId);
    }

    [Fact]
    public async Task Import_NewPageForTakenLanguage_IsImportedAsLandingPageWithError()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "nl-NL");
        var payload = JsonConvert.SerializeObject(new { PageBuilderPages = new[] { ExportedPage("g-nl", "nl-NL", "/over-ons", root.Id) } });

        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var progress = new List<ExportImportProgressInfo>();
        await fixture.CreateExportImport().DoImportAsync(input, progress.Add, TestContext.Current.CancellationToken);

        Assert.Null((await fixture.LoadGroupAsync("g-nl")).MultilanguagePageId);
        Assert.Contains(progress.SelectMany(x => x.Errors), x => x.Contains("g-nl"));
    }

    [Fact]
    public async Task ConvertAsync_PageWithoutStoreLanguage_ThrowsAndCreatesNoRoot()
    {
        await using var fixture = await Fixture.CreateAsync();
        var noLanguage = await fixture.CreateLanguageAsync(null, null, permalink: "/faq");
        var unknownLanguage = await fixture.CreateLanguageAsync(null, "xx-XX", permalink: "/faq");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.ConvertAsync(noLanguage.Id, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.ConvertAsync(unknownLanguage.Id, TestContext.Current.CancellationToken));

        await using var context = fixture.CreateContext();
        Assert.False(await context.Set<MultilanguagePageEntity>().AnyAsync(TestContext.Current.CancellationToken));
        Assert.Null((await fixture.LoadGroupAsync(noLanguage.Id)).MultilanguagePageId);
    }

    [Fact]
    public async Task SaveChangesAsync_ExistingRootWithoutDefaultCulture_KeepsStoredDefaultCulture()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = new MultilanguagePage { StoreId = StoreId, Name = "FAQ", Permalink = "/faq", DefaultCultureName = "nl-NL" };
        await fixture.Roots.SaveChangesAsync([root]);

        var update = await fixture.Roots.GetByIdAsync(root.Id);
        update.DefaultCultureName = null;
        update.Name = "Renamed";
        await fixture.Roots.SaveChangesAsync([update]);

        Assert.Equal("nl-NL", (await fixture.Roots.GetByIdAsync(root.Id)).DefaultCultureName);
    }

    [Fact]
    public async Task UpdateAsync_DefaultCultureWithoutVersionOrNotAStoreLanguage_ThrowsAndKeepsStoredValue()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US");
        await fixture.CreateLanguageAsync(null, "fr-FR", permalink: "/faq");

        var noVersion = await fixture.Roots.GetByIdAsync(root.Id);
        noVersion.DefaultCultureName = "nl-NL";
        var notAStoreLanguage = await fixture.Roots.GetByIdAsync(root.Id);
        notAStoreLanguage.DefaultCultureName = "fr-FR";

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.UpdateAsync(noVersion, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Roots.UpdateAsync(notAStoreLanguage, TestContext.Current.CancellationToken));

        Assert.Equal("en-US", (await fixture.Roots.GetByIdAsync(root.Id)).DefaultCultureName);
    }

    [Fact]
    public async Task UpdateAsync_DefaultCultureWithVersion_IsSaved()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US");
        await fixture.CreateLanguageAsync(root, "nl-NL");

        var update = await fixture.Roots.GetByIdAsync(root.Id);
        update.DefaultCultureName = "NL-nl";
        update.StoreId = "another-store";
        await fixture.Roots.UpdateAsync(update, TestContext.Current.CancellationToken);

        var saved = await fixture.Roots.GetByIdAsync(root.Id);
        Assert.Equal(("nl-NL", StoreId), (saved.DefaultCultureName, saved.StoreId));
    }

    [Fact]
    public async Task UnlinkAsync_ClearsSourceRevision()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        await fixture.PublishNewDraftAsync(en.Id, content: null);
        Assert.NotNull((await fixture.LoadGroupAsync(en.Id)).SourceRevisionDate);

        await fixture.Roots.UnlinkAsync(en.Id, TestContext.Current.CancellationToken);

        Assert.Null((await fixture.LoadGroupAsync(en.Id)).SourceRevisionDate);
    }

    [Fact]
    public async Task List_ObjectIds_ListsOnlyRequestedPages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync();
        await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var landing = await fixture.CreateLanguageAsync(null, "en-US", permalink: "/summer", name: "Summer", draftContent: Content("summer"));

        var result = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, ObjectIds = [landing.Id], Take = 10 },
            TestContext.Current.CancellationToken);

        Assert.Equal([landing.Id], result.Results.Select(x => x.Id));
    }

    [Fact]
    public async Task Search_KeywordIgnoresCase_ForLandingAndMultilanguagePages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync("Searchable root");
        var landing = await fixture.CreateLanguageAsync(null, "en-US", permalink: "/summer", name: "qa-searchable-landing", draftContent: Content("summer"));

        // Upper case first: a search answered from the cache must not hide a case-sensitive query.
        var list = await fixture.List.SearchAsync(
            new PageBuilderPageSearchCriteria { StoreId = StoreId, Keyword = "SEARCHABLE", Take = 10 },
            TestContext.Current.CancellationToken);
        var groups = await fixture.GroupSearch.SearchAsync(new PageBuilderPageSearchCriteria { StoreId = StoreId, Keyword = "SEARCHABLE", Take = 10 });

        Assert.Equal(new[] { landing.Id, root.Id }.Order(), list.Results.Select(x => x.Id).Order());
        Assert.Equal([landing.Id], groups.Results.Select(x => x.Id));
    }

    [Fact]
    public async Task MultilanguagePageSearch_KeywordIgnoresCase()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateRootAsync("Searchable root");
        await fixture.CreateRootAsync("Other");

        var byName = await fixture.RootSearch.SearchAsync(new MultilanguagePageSearchCriteria { StoreId = StoreId, Keyword = "SEARCHABLE", Take = 10 });
        var byPermalink = await fixture.RootSearch.SearchAsync(new MultilanguagePageSearchCriteria { StoreId = StoreId, Keyword = "ABOUT-US", Take = 10 });

        Assert.Equal([root.Id], byName.Results.Select(x => x.Id));
        Assert.Equal(2, byPermalink.TotalCount);
    }

    [Fact]
    public async Task PublishAllAsync_LanguageFails_PublishesOthersAndReportsFailure()
    {
        await using var fixture = await Fixture.CreateAsync(failPublishingCultureName: "nl-NL");
        var root = await fixture.CreateRootAsync();
        var en = await fixture.CreateLanguageAsync(root, "en-US", draftContent: Content("en"));
        var nl = await fixture.CreateLanguageAsync(root, "nl-NL", draftContent: Content("nl"));
        var de = await fixture.CreateLanguageAsync(root, "de-DE", draftContent: Content("de"));

        var result = await fixture.Roots.PublishAllAsync(root.Id, TestContext.Current.CancellationToken);

        Assert.Equal(["de-DE", "en-US"], result.ProcessedCultureNames.Order());
        Assert.Equal([("nl-NL", MultilanguagePageBatchResult.FailedReason)], result.Skipped.Select(x => (x.CultureName, x.Reason)));
        Assert.Equal("Published", (await fixture.Groups.GetByIdAsync(de.Id)).Status);
        Assert.Equal("Draft", (await fixture.Groups.GetByIdAsync(nl.Id)).Status);
        Assert.NotNull(en);
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

        private Fixture(string connectionString, SqliteConnection anchorConnection, string failPublishingCultureName)
        {
            _connectionString = connectionString;
            _anchorConnection = anchorConnection;

            Func<IPageBuilderModuleRepository> repositoryFactory = () => new PageBuilderModuleRepository(CreateContext());
            var events = new NoopEventPublisher();
            Groups = new FailingPublishGroupedPageService(
                repositoryFactory,
                () => throw new InvalidOperationException("Content repository is not used by these tests."),
                _cache,
                events,
                NullLogger<GroupedPageService>.Instance,
                failPublishingCultureName);
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
                new PageBuilderPageService(repositoryFactory, _cache, events),
                Lock,
                NullLogger<MultilanguagePageService>.Instance);
            RootSearch = new MultilanguagePageSearchService(repositoryFactory, _cache, Roots, Options.Create(new CrudOptions()));
            List = new PageBuilderPageListService(repositoryFactory, groupSearch, Groups, Roots, storeService);
        }

        public FakeStoreService StoreService { get; }

        public InProcessDistributedLock Lock { get; } = new();

        public GroupedPageService Groups { get; }

        public MultilanguagePageService Roots { get; }

        public MultilanguagePageSearchService RootSearch { get; }

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

            return new PageBuilderExportImport(Groups, GroupSearch, null!, sharedComponents, null!, jsonSerializer,
                new MultilanguagePageExportImport(Roots, null, jsonSerializer));
        }

        /// <param name="failPublishingCultureName">Saving a published version of this language fails, like a storage error would.</param>
        public static async Task<Fixture> CreateAsync(string failPublishingCultureName = null)
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
            var fixture = new Fixture(connectionString, anchorConnection, failPublishingCultureName);

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

    private sealed class FailingPublishGroupedPageService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        Func<IContentStreamRepository> contentStreamRepositoryFactory,
        TestPlatformMemoryCache cache,
        IEventPublisher eventPublisher,
        Microsoft.Extensions.Logging.ILogger<GroupedPageService> logger,
        string failPublishingCultureName)
        : GroupedPageService(repositoryFactory, contentStreamRepositoryFactory, cache, eventPublisher, logger)
    {
        public override Task SaveChangesAsync(IList<GroupedPageBuilderPage> models)
        {
            if (failPublishingCultureName != null &&
                models.Any(x => x.CultureName == failPublishingCultureName && x.Pages.Any(p => p.Status == "Published")))
            {
                throw new InvalidOperationException("Storage is unavailable.");
            }

            return base.SaveChangesAsync(models);
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
