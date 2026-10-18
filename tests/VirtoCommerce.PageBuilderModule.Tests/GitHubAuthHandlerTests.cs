using System;
using System.Buffers.Text;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// How the git content client signs its requests: the configured token, or — with a GitHub App — a token
    /// of the App's installation on the organization the request acts on, minted once and reused until it is
    /// about to expire.
    /// </summary>
    public class GitHubAuthHandlerTests
    {
        private static readonly RSA Key = RSA.Create(2048);

        [Fact]
        public async Task With_a_token_every_request_carries_it()
        {
            var github = new GitHubScript(("GET", "repos/acme/site-a", (request, _) =>
            {
                Assert.Equal("Bearer t0ken", request.Headers.Authorization?.ToString());
                return GitHubScript.Json("{}");
            }));

            await Client(github, new GitContentOptions { Token = "t0ken" }).GetAsync("repos/acme/site-a", TestContext.Current.CancellationToken);

            github.AssertDone();
        }

        [Fact]
        public async Task As_an_App_it_mints_the_installation_token_of_the_owner_once_and_reuses_it()
        {
            var github = new GitHubScript(
                ("GET", "orgs/acme/installation", (request, _) =>
                {
                    Assert.Equal(3, request.Headers.Authorization!.Parameter!.Split('.').Length); // the App's own JWT
                    return GitHubScript.Json("""{ "id": 42 }""");
                }),
                ("POST", "app/installations/42/access_tokens", (_, _) => GitHubScript.Json("""{ "token": "ghs_one", "expires_at": "2099-01-01T00:00:00Z" }""", HttpStatusCode.Created)),
                ("GET", "repos/acme/site-a", (request, _) => Authorized(request, "ghs_one")),
                ("GET", "repos/acme/site-b/contents/pages/x.page?ref=main", (request, _) => Authorized(request, "ghs_one")));
            var client = Client(github, App());

            await client.GetAsync("repos/acme/site-a", TestContext.Current.CancellationToken);
            await client.GetAsync("repos/acme/site-b/contents/pages/x.page?ref=main", TestContext.Current.CancellationToken);

            github.AssertDone();
        }

        /// <summary>A GraphQL url names no repository, so the request says whose installation it needs.</summary>
        [Fact]
        public async Task As_an_App_a_GraphQL_request_is_signed_for_the_owner_it_names()
        {
            var github = new GitHubScript(
                ("GET", "orgs/other/installation", (_, _) => GitHubScript.Json("""{ "id": 7 }""")),
                ("POST", "app/installations/7/access_tokens", (_, _) => GitHubScript.Json("""{ "token": "ghs_other", "expires_at": "2099-01-01T00:00:00Z" }""", HttpStatusCode.Created)),
                ("POST", "graphql", (request, _) => Authorized(request, "ghs_other")));

            using var request = new HttpRequestMessage(HttpMethod.Post, "graphql") { Content = new StringContent("{}") };
            request.Options.Set(GitHubAuthHandler.Owner, "other");
            await Client(github, App()).SendAsync(request, TestContext.Current.CancellationToken);

            github.AssertDone();
        }

        [Fact]
        public async Task As_an_App_a_refused_token_is_minted_again_once()
        {
            var github = new GitHubScript(
                ("GET", "orgs/acme/installation", (_, _) => GitHubScript.Json("""{ "id": 42 }""")),
                ("POST", "app/installations/42/access_tokens", (_, _) => GitHubScript.Json("""{ "token": "ghs_revoked", "expires_at": "2099-01-01T00:00:00Z" }""", HttpStatusCode.Created)),
                ("GET", "repos/acme/site-a", (_, _) => GitHubScript.Status(HttpStatusCode.Unauthorized)),
                ("GET", "orgs/acme/installation", (_, _) => GitHubScript.Json("""{ "id": 42 }""")),
                ("POST", "app/installations/42/access_tokens", (_, _) => GitHubScript.Json("""{ "token": "ghs_fresh", "expires_at": "2099-01-01T00:00:00Z" }""", HttpStatusCode.Created)),
                ("GET", "repos/acme/site-a", (request, _) => Authorized(request, "ghs_fresh")));

            var response = await Client(github, App()).GetAsync("repos/acme/site-a", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            github.AssertDone();
        }

        [Fact]
        public async Task As_an_App_not_installed_on_the_owner_it_says_so()
        {
            var github = new GitHubScript(
                ("GET", "orgs/acme/installation", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)),
                ("GET", "users/acme/installation", (_, _) => GitHubScript.Status(HttpStatusCode.NotFound)));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Client(github, App()).GetAsync("repos/acme/site-a", TestContext.Current.CancellationToken));

            Assert.Contains("not installed on \"acme\"", error.Message);
        }

        /// <summary>The one-click setup can replace the App while the client factory keeps this handler alive.</summary>
        [Fact]
        public async Task A_new_App_signs_with_its_own_token_not_the_old_ones()
        {
            var github = new GitHubScript(
                ("GET", "orgs/acme/installation", (_, _) => GitHubScript.Json("""{ "id": 42 }""")),
                ("POST", "app/installations/42/access_tokens", (_, _) => GitHubScript.Json("""{ "token": "ghs_old", "expires_at": "2099-01-01T00:00:00Z" }""", HttpStatusCode.Created)),
                ("GET", "repos/acme/site-a", (request, _) => Authorized(request, "ghs_old")),
                ("GET", "orgs/acme/installation", (_, _) => GitHubScript.Json("""{ "id": 43 }""")),
                ("POST", "app/installations/43/access_tokens", (_, _) => GitHubScript.Json("""{ "token": "ghs_new", "expires_at": "2099-01-01T00:00:00Z" }""", HttpStatusCode.Created)),
                ("GET", "repos/acme/site-a", (request, _) => Authorized(request, "ghs_new")));
            var connection = new Switchable { Value = App() };
            var client = new HttpClient(new GitHubAuthHandler(new MemoryCache(new MemoryCacheOptions()), connection) { InnerHandler = github })
            {
                BaseAddress = new Uri("https://api.github.test/"),
            };

            await client.GetAsync("repos/acme/site-a", TestContext.Current.CancellationToken);
            var replaced = App();
            replaced.AppId = "67890";
            connection.Value = replaced;
            await client.GetAsync("repos/acme/site-a", TestContext.Current.CancellationToken);

            github.AssertDone();
        }

        [Fact]
        public void The_App_credential_is_a_signed_JWT_that_GitHub_accepts()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

            // as an environment variable holds it: one line, "\n" escapes
            var jwt = GitHubAuthHandler.AppJwt("12345", Key.ExportPkcs8PrivateKeyPem().Replace("\n", "\\n"), now);

            var parts = jwt.Split('.');
            Assert.True(Key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2]),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            var header = JObject.Parse(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[0])));
            var payload = JObject.Parse(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1])));
            Assert.Equal("RS256", header["alg"]?.Value<string>());
            Assert.Equal("12345", payload["iss"]?.Value<string>());
            Assert.Equal(now.ToUnixTimeSeconds() - 60, payload["iat"]?.Value<long>());
            Assert.Equal(now.ToUnixTimeSeconds() + 540, payload["exp"]?.Value<long>());
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        private static GitContentOptions App() => new()
        {
            AppId = "12345",
            PrivateKey = Key.ExportPkcs8PrivateKeyPem(),
            ApiUrl = new Uri("https://api.github.test/"),
        };

        private static HttpClient Client(GitHubScript github, GitContentOptions options) =>
            new(new GitHubAuthHandler(new MemoryCache(new MemoryCacheOptions()), Options.Create(options)) { InnerHandler = github })
            {
                BaseAddress = new Uri("https://api.github.test/"),
            };

        private sealed class Switchable : IOptions<GitContentOptions>
        {
            public GitContentOptions Value { get; set; }
        }

        private static HttpResponseMessage Authorized(HttpRequestMessage request, string token)
        {
            Assert.Equal($"Bearer {token}", request.Headers.Authorization?.ToString());
            return GitHubScript.Json("{}");
        }
    }
}
