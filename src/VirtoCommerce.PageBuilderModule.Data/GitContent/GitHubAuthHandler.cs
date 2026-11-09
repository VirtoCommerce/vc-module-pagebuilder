using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>Authenticates GitHub requests with a token or a GitHub App installation token.</summary>
public class GitHubAuthHandler(IGitHubConnectionService gitHubConnectionService, IMemoryCache memoryCache, IOptions<GitContentOptions> options)
    : DelegatingHandler
{
    /// <summary>Request option naming the owner for urls without one (GraphQL).</summary>
    public static readonly HttpRequestOptionsKey<string> OwnerOptionKey = new("PageBuilderGitHubOwner");

    private const string BearerScheme = "Bearer";

    private static readonly TimeSpan RefreshBeforeExpiry = TimeSpan.FromMinutes(5);

    // the App's JWT is issued this far in the past, against clock drift between the platform and GitHub
    private static readonly TimeSpan JwtClockDrift = TimeSpan.FromSeconds(60);

    // GitHub refuses an App JWT that expires more than ten minutes ahead
    private static readonly TimeSpan JwtLifetime = TimeSpan.FromMinutes(9);

    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromHours(1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // read once per request: the connection can change while the handler lives
        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
        if (!connection.UsesApp)
        {
            if (!string.IsNullOrWhiteSpace(connection.Token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, connection.Token);
            }

            return await base.SendAsync(request, cancellationToken);
        }

        var owner = GetOwner(request, connection);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, await GetTokenAsync(connection, owner, cancellationToken));
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // revoked or expired early: retry once with a new token
        response.Dispose();
        memoryCache.Remove(GetTokenCacheKey(connection.AppId, owner));
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, await GetTokenAsync(connection, owner, cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }

    protected virtual string GetOwner(HttpRequestMessage request, GitHubConnection connection)
    {
        // also under a GitHub Enterprise "/api/v3/" prefix
        var segments = request.RequestUri?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] is "repos" or "orgs")
            {
                return Uri.UnescapeDataString(segments[i + 1]);
            }
        }

        if (request.Options.TryGetValue(OwnerOptionKey, out var owner) && !string.IsNullOrWhiteSpace(owner))
        {
            return owner;
        }

        var fallback = connection.Organization ?? connection.Repository?.Split('/')[0];
        return string.IsNullOrWhiteSpace(fallback)
            ? throw new GitContentException($"Cannot tell which GitHub account the request to {request.RequestUri} acts on.")
            : fallback.Trim();
    }

    protected virtual async Task<string> GetTokenAsync(GitHubConnection connection, string owner, CancellationToken cancellationToken)
    {
        var cacheKey = GetTokenCacheKey(connection.AppId, owner);
        if (memoryCache.TryGetValue<string>(cacheKey, out var cached))
        {
            return cached;
        }

        var jwt = CreateAppJwt(connection.AppId, connection.PrivateKey, DateTimeOffset.UtcNow);
        var escapedOwner = Uri.EscapeDataString(owner);

        // organization installation, else user installation
        var installation = await SendAsAppAsync<Installation>(HttpMethod.Get, $"orgs/{escapedOwner}/installation", jwt, cancellationToken)
                           ?? await SendAsAppAsync<Installation>(HttpMethod.Get, $"users/{escapedOwner}/installation", jwt, cancellationToken)
                           ?? throw new GitContentException(
                               $"The GitHub App {connection.AppId} is not installed on \"{owner}\". Install it there with access to all repositories.");

        var accessToken = await SendAsAppAsync<InstallationToken>(HttpMethod.Post, $"app/installations/{installation.Id}/access_tokens", jwt, cancellationToken)
                          ?? throw new GitContentException($"GitHub did not issue a token for the App's installation on \"{owner}\".");

        var expiresAt = accessToken.ExpiresAt is { } expires
            ? new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc))
            : DateTimeOffset.UtcNow.Add(DefaultTokenLifetime);

        memoryCache.Set(cacheKey, accessToken.Token, expiresAt - RefreshBeforeExpiry);
        return accessToken.Token;
    }

    /// <summary>Sends a request signed as the App itself; <c>null</c> on 404.</summary>
    private async Task<T> SendAsAppAsync<T>(HttpMethod method, string relativeUrl, string jwt, CancellationToken cancellationToken)
        where T : class
    {
        var operation = $"authenticate as the GitHub App ({method} {relativeUrl})";

        // built here: the client's default headers do not apply
        using var request = new HttpRequestMessage(method, new Uri(new Uri(options.Value.ApiUrl.AbsoluteUri.TrimEnd('/') + "/"), relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, jwt);
        GitHubApiClient.AddDefaultHeaders(request.Headers);

        using var response = await base.SendAsync(request, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await GitHubApiClient.ReadAsAsync<T>(response, operation, cancellationToken);
    }

    private static string GetTokenCacheKey(string appId, string owner) => $"{nameof(GitHubAuthHandler)}:{appId}:{owner.ToLowerInvariant()}";

    /// <summary>Creates the App JWT (RS256).</summary>
    internal static string CreateAppJwt(string appId, string privateKey, DateTimeOffset now)
    {
        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(privateKey.Replace("\\n", "\n"));
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            throw new GitContentException(
                $"The private key of the GitHub App {appId} is not a PEM private key. Check PageBuilder:GitContent:PrivateKey, or set the App up again.", ex);
        }

        // a provider cached across calls would keep the RSA instance disposed here
        var key = new RsaSecurityKey(rsa) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } };
        var payload = new JwtPayload
        {
            { JwtRegisteredClaimNames.Iat, (now - JwtClockDrift).ToUnixTimeSeconds() },
            { JwtRegisteredClaimNames.Exp, (now + JwtLifetime).ToUnixTimeSeconds() },
            { JwtRegisteredClaimNames.Iss, appId },
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.RsaSha256)), payload));
    }

    private sealed class Installation
    {
        [JsonProperty("id")]
        public long Id { get; set; }
    }

    private sealed class InstallationToken
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("expires_at")]
        public DateTime? ExpiresAt { get; set; }
    }
}
