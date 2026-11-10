using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class FilePageDraftServiceTests
{
    private readonly FakeBlobStorage _live = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DiscardBlobDraftAsync_PublishedPage_RemovesTheDraftAndRaisesTheEvent()
    {
        _live.Files["pages/s1/about.page"] = "live";
        _live.Files["pages/s1/about.page-draft"] = "draft";

        var status = await Service().DiscardBlobDraftAsync("s1", "pages", "about.page", theme: null, Ct);

        Assert.Equal(DraftDiscardStatus.Discarded, status);
        Assert.False(_live.Files.ContainsKey("pages/s1/about.page-draft"));
        Assert.Equal(["about.page"], _live.Events.OfType<PageBuilderContentChangedEvent>().Single().ChangedEntries.Select(x => x.NewEntry.Path));
    }

    [Fact]
    public async Task DiscardBlobDraftAsync_PageNeverPublished_KeepsTheDraft()
    {
        _live.Files["pages/s1/about.page-draft"] = "draft";

        var status = await Service().DiscardBlobDraftAsync("s1", "pages", "about.page", theme: null, Ct);

        Assert.Equal(DraftDiscardStatus.OnlyCopy, status);
        Assert.True(_live.Files.ContainsKey("pages/s1/about.page-draft"));
        Assert.Empty(_live.Events);
    }

    [Fact]
    public async Task DiscardBlobDraftAsync_NoDraft_ChangesNothing()
    {
        _live.Files["pages/s1/about.page"] = "live";

        var status = await Service().DiscardBlobDraftAsync("s1", "pages", "about.page", theme: null, Ct);

        Assert.Equal(DraftDiscardStatus.NoDraft, status);
        Assert.Empty(_live.Events);
    }

    private FilePageDraftService Service()
    {
        var options = Options.Create(new GitContentOptions());
        return TestServices.FilePageDrafts(_live, options, GitHubConnections.CreateService(options.Value), new FakeGitContentPolicy(enabled: false));
    }
}
