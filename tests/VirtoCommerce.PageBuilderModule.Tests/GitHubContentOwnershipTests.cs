using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GitHubContentOwnershipTests
{
    private const string Repo = "acme/site-s1";

    [Fact]
    public async Task GetStoreIdAsync_DescriptionNamesStore_ReturnsStoreIdAndCachesIt()
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => Repository(GitHubContentOwnership.DescriptionPrefix + "s1")));
        var ownership = Ownership(github);

        Assert.Equal("s1", await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
        Assert.Equal("s1", await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));

        github.AssertDone();
    }

    [Fact]
    public async Task GetStoreIdAsync_RepositoryNotCreatedByModule_ReturnsNull()
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => Repository("Our web site")));

        Assert.Null(await Ownership(github).GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetStoreIdAsync_UnownedRepository_ExpiresSoonerThanOwnedOne()
    {
        // The module may adopt an unowned repository on another server at any moment.
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => Repository("Our web site")),
            ("GET", $"repos/{Repo}", (_, _) => Repository(GitHubContentOwnership.DescriptionPrefix + "s1")));
        var clock = new TestClock();
        var ownership = Ownership(github, clock);

        Assert.Null(await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
        clock.UtcNow += TimeSpan.FromMinutes(2);
        Assert.Equal("s1", await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
        clock.UtcNow += TimeSpan.FromMinutes(2);
        Assert.Equal("s1", await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));

        github.AssertDone();
    }

    [Fact]
    public async Task GetStoreIdAsync_NameInOtherCase_SharesCacheEntry()
    {
        // GitHub repository names are case-insensitive.
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => Repository(GitHubContentOwnership.DescriptionPrefix + "s1")),
            ("GET", $"repos/{Repo}", (_, _) => Repository(GitHubContentOwnership.DescriptionPrefix + "s1")));
        var ownership = Ownership(github);

        await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken);
        Assert.Equal("s1", await ownership.GetStoreIdAsync("Acme/Site-S1", TestContext.Current.CancellationToken));
        ownership.Invalidate("ACME/SITE-S1");
        await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken);

        github.AssertDone();
    }

    [Fact]
    public async Task GetStoreIdAsync_RepositoryNotFound_DoesNotCacheAnswer()
    {
        var github = new GitHubScript(
            ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
            ("GET", $"repos/{Repo}", (_, _) => Repository(GitHubContentOwnership.DescriptionPrefix + "s1")));
        var ownership = Ownership(github);

        Assert.Null(await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
        Assert.Equal("s1", await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));

        github.AssertDone();
    }

    [Fact]
    public async Task GetStoreIdAsync_GitHubFails_ThrowsGitContentException()
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.BadGateway)));

        await Assert.ThrowsAsync<GitContentException>(() => Ownership(github).GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
    }

    private static GitHubContentOwnership Ownership(GitHubScript github, TestClock clock = null) =>
        new(new GitHubApiClient(github), new MemoryCache(new MemoryCacheOptions { Clock = clock ?? new TestClock() }), Options.Create(new GitContentOptions
        {
            Enabled = true,
            Organization = "acme",
            Token = "t0ken",
            BaseBranch = "main",
            ReadCacheExpiration = TimeSpan.FromMinutes(1),
            ImmutableReadCacheExpiration = TimeSpan.FromHours(1),
        }));

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    }

    private static HttpResponseMessage Repository(string description) =>
        GitHubScript.Json(new JObject { ["full_name"] = Repo, ["description"] = description }.ToString());
}
