using System;
using System.Buffers.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// Authenticates every request of the git content client.
    /// <para>
    /// With a token configured, that token. With a GitHub App, a token of the App's installation on the
    /// owner the request acts on — each site's repository belongs to an organization that installed the
    /// App, and its installation token is what may write there. Installation tokens live an hour; they are
    /// cached per owner and minted again five minutes before they expire, or at once when GitHub refuses one.
    /// </para>
    /// </summary>
    public sealed class GitHubAuthHandler : DelegatingHandler
    {
        /// <summary>
        /// The owner a request acts on, for urls that name none — the GraphQL endpoint. Requests to
        /// "repos/{owner}/..." and "orgs/{owner}..." carry it in their url.
        /// </summary>
        public static readonly HttpRequestOptionsKey<string> Owner = new("PageBuilderGitHubOwner");

        private static readonly TimeSpan RefreshBeforeExpiry = TimeSpan.FromMinutes(5);

        private readonly IMemoryCache _memoryCache;
        private readonly IOptions<GitContentOptions> _options;

        public GitHubAuthHandler(IMemoryCache memoryCache, IOptions<GitContentOptions> options)
        {
            _memoryCache = memoryCache;
            _options = options;
        }

        // read per use: the client factory keeps a handler for minutes, and the connection can change meanwhile
        private GitContentOptions Current => _options.Value;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!Current.UsesApp)
            {
                if (!string.IsNullOrWhiteSpace(Current.Token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Current.Token);
                }

                return await base.SendAsync(request, cancellationToken);
            }

            var owner = OwnerOf(request);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(owner, cancellationToken));
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            // the cached token was revoked or expired early: one fresh token, one more try
            response.Dispose();
            _memoryCache.Remove(TokenKey(Current.AppId, owner));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(owner, cancellationToken));
            return await base.SendAsync(request, cancellationToken);
        }

        private string OwnerOf(HttpRequestMessage request)
        {
            // "repos/{owner}/{name}/..." or "orgs/{owner}..." — also under a GitHub Enterprise "/api/v3/" prefix
            var segments = request.RequestUri?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i] is "repos" or "orgs")
                {
                    return Uri.UnescapeDataString(segments[i + 1]);
                }
            }

            if (request.Options.TryGetValue(Owner, out var owner) && !string.IsNullOrWhiteSpace(owner))
            {
                return owner;
            }

            var fallback = Current.Organization ?? Current.Repository?.Split('/')[0];
            return string.IsNullOrWhiteSpace(fallback)
                ? throw new InvalidOperationException($"Cannot tell which GitHub account the request to {request.RequestUri} acts on.")
                : fallback.Trim();
        }

        private async Task<string> GetTokenAsync(string owner, CancellationToken cancellationToken)
        {
            if (_memoryCache.TryGetValue<string>(TokenKey(Current.AppId, owner), out var cached))
            {
                return cached;
            }

            var jwt = AppJwt(Current.AppId, Current.PrivateKey, DateTimeOffset.UtcNow);

            // an organization's installation, else a user's — the App can be installed on either
            var installation = await SendAsAppAsync(HttpMethod.Get, $"orgs/{Uri.EscapeDataString(owner)}/installation", jwt, cancellationToken)
                               ?? await SendAsAppAsync(HttpMethod.Get, $"users/{Uri.EscapeDataString(owner)}/installation", jwt, cancellationToken)
                               ?? throw new InvalidOperationException(
                                   $"The GitHub App {Current.AppId} is not installed on \"{owner}\". Install it there with access to all repositories.");

            var minted = await SendAsAppAsync(HttpMethod.Post, $"app/installations/{installation["id"]}/access_tokens", jwt, cancellationToken)
                         ?? throw new InvalidOperationException($"GitHub did not issue a token for the App's installation on \"{owner}\".");

            var token = minted["token"]?.Value<string>();
            var expiresAt = minted["expires_at"]?.Value<DateTime?>() is { } expires
                ? new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc))
                : DateTimeOffset.UtcNow.AddHours(1);

            _memoryCache.Set(TokenKey(Current.AppId, owner), token, expiresAt - RefreshBeforeExpiry);
            return token;
        }

        /// <summary>A request signed as the App itself; <c>null</c> on 404.</summary>
        private async Task<JObject> SendAsAppAsync(HttpMethod method, string relativeUrl, string jwt, CancellationToken cancellationToken)
        {
            // built here, so the client's default headers do not apply: GitHub needs a User-Agent
            using var request = new HttpRequestMessage(method, new Uri(new Uri(Current.ApiUrl.AbsoluteUri.TrimEnd('/') + "/"), relativeUrl));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd("vc-pagebuilder-module");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"GitHub App authentication failed ({method} {relativeUrl}): {(int)response.StatusCode} {response.ReasonPhrase}. {(body.Length > 300 ? body[..300] : body)}",
                    null, response.StatusCode);
            }

            return JObject.Parse(body);
        }

        // per App too: a new App connected in place of the old one must not use the old one's tokens
        private static string TokenKey(string appId, string owner) => $"{nameof(GitHubAuthHandler)}:{appId}:{owner.ToLowerInvariant()}";

        /// <summary>
        /// The App's own credential: an RS256 JWT, issued a minute in the past against clock drift and
        /// valid for nine minutes (GitHub accepts ten at most).
        /// </summary>
        internal static string AppJwt(string appId, string privateKey, DateTimeOffset now)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey.Replace("\\n", "\n"));

            var header = Base64Url.EncodeToString("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"u8);
            var payload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new JObject
            {
                ["iat"] = now.AddSeconds(-60).ToUnixTimeSeconds(),
                ["exp"] = now.AddMinutes(9).ToUnixTimeSeconds(),
                ["iss"] = appId,
            }.ToString(Formatting.None)));
            var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return $"{header}.{payload}.{Base64Url.EncodeToString(signature)}";
        }
    }
}
