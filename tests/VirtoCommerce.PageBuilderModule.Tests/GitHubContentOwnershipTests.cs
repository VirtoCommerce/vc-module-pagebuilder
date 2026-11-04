using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>Whose a repository is: the store its description names, as the module writes it when it creates one.</summary>
    public class GitHubContentOwnershipTests
    {
        private const string Repo = "acme/site-s1";

        [Fact]
        public async Task A_repository_belongs_to_the_store_its_description_names_and_stays_so()
        {
            var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => Repository(GitHubContentOwnership.Marker + "s1")));
            var ownership = Ownership(github);

            Assert.Equal("s1", await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
            Assert.Equal("s1", await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));

            github.AssertDone();
        }

        [Fact]
        public async Task A_repository_the_module_did_not_create_is_nobodys()
        {
            var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => Repository("Our web site")));

            Assert.Null(await Ownership(github).GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
        }

        /// <summary>One not made yet may be made for a store any moment: asked again, not remembered as nobody's.</summary>
        [Fact]
        public async Task A_repository_not_made_yet_is_asked_about_again()
        {
            var github = new GitHubScript(
                ("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
                ("GET", $"repos/{Repo}", (_, _) => Repository(GitHubContentOwnership.Marker + "s1")));
            var ownership = Ownership(github);

            Assert.Null(await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
            Assert.Equal("s1", await ownership.GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));

            github.AssertDone();
        }

        [Fact]
        public async Task GitHub_refusing_to_say_is_an_error()
        {
            var github = new GitHubScript(("GET", $"repos/{Repo}", (_, _) => GitHubScript.Status(HttpStatusCode.BadGateway)));

            await Assert.ThrowsAsync<HttpRequestException>(() => Ownership(github).GetStoreIdAsync(Repo, TestContext.Current.CancellationToken));
        }

        private static GitHubContentOwnership Ownership(GitHubScript github) =>
            new(github, new MemoryCache(new MemoryCacheOptions()), Options.Create(new GitContentOptions { Enabled = true, Organization = "acme", Token = "t0ken", BaseBranch = "main" }));

        private static HttpResponseMessage Repository(string description) =>
            GitHubScript.Json(new JObject { ["full_name"] = Repo, ["description"] = description }.ToString());
    }
}
