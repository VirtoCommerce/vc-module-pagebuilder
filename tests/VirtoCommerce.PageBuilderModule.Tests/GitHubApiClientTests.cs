using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GitHubApiClientTests
{
    private const string Repo = "acme/site-s1";

    [Fact]
    public async Task GetRepositoryAsync_TransportFailure_ThrowsGitContentException()
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => throw new HttpRequestException("No such host is known.")));

        var error = await Assert.ThrowsAsync<GitContentException>(() => new GitHubApiClient(github).GetRepositoryAsync(Repo, TestContext.Current.CancellationToken));

        Assert.IsType<HttpRequestException>(error.InnerException);
        Assert.Contains("No such host is known.", error.Message);
    }

    [Fact]
    public async Task GetRepositoryAsync_Timeout_ThrowsGitContentException()
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.")));

        var error = await Assert.ThrowsAsync<GitContentException>(() => new GitHubApiClient(github).GetRepositoryAsync(Repo, TestContext.Current.CancellationToken));

        Assert.Contains("HttpClient.Timeout", error.Message);
    }

    [Fact]
    public async Task GetRepositoryAsync_CallerCancelled_ThrowsOperationCanceledException()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => throw new TaskCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitHubApiClient(github).GetRepositoryAsync(Repo, cancelled.Token));
    }

    [Fact]
    public async Task GetRepositoryAsync_MalformedResponse_ThrowsGitContentException()
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => GitHubScript.Json("<html>")));

        await Assert.ThrowsAsync<GitContentException>(() => new GitHubApiClient(github).GetRepositoryAsync(Repo, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetRepositoryAsync_Missing_ReturnsNull()
    {
        var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

        Assert.Null(await new GitHubApiClient(github).GetRepositoryAsync(Repo, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRepositoryAsync_AlreadyExists_ReturnsNull()
    {
        var github = new GitHubScript(("POST", "orgs/acme/repos", (_, _) =>
            GitHubScript.Json("""{ "errors": [ { "message": "name already exists on this account" } ] }""", HttpStatusCode.UnprocessableEntity)));

        Assert.Null(await new GitHubApiClient(github).CreateRepositoryAsync(Repo, new JObject { ["name"] = "site-s1" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PostGraphQlAsync_ErrorsInSuccessfulResponse_ThrowsGitContentException()
    {
        var github = new GitHubScript(("POST", "graphql", (request, _) =>
        {
            Assert.True(request.Options.TryGetValue(GitHubAuthHandler.OwnerOptionKey, out var owner));
            Assert.Equal("acme", owner);
            return GitHubScript.Json("""{ "errors": [ { "message": "Something went wrong" } ] }""");
        }));

        var error = await Assert.ThrowsAsync<GitContentException>(() => new GitHubApiClient(github)
            .PostGraphQlAsync(new Uri("https://api.github.test/graphql"), "acme", new JObject(), "read history", TestContext.Current.CancellationToken));

        Assert.Contains("Something went wrong", error.Message);
        github.AssertDone();
    }
}
