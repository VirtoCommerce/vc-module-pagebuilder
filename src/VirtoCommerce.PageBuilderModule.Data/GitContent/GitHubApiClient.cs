using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

public class GitHubApiClient(IHttpClientFactory httpClientFactory) : IGitHubApiClient
{
    public const string HttpClientName = "PageBuilderGitContent";

    public const string AnonymousHttpClientName = "PageBuilderGitHubSetup";

    public const string MediaType = "application/vnd.github+json";

    public const string ApiVersionHeaderName = "X-GitHub-Api-Version";

    public const string ApiVersion = "2022-11-28";

    public const string UserAgent = "vc-pagebuilder-module";

    private const string JsonMediaType = "application/json";

    private const string AlreadyExistsError = "already exists";

    private const string GraphQlNotFoundError = "NOT_FOUND";

    private const string GraphQlRepositoryField = "repository";

    private const string BlobMode = "100644";

    private const string BlobType = "blob";

    // enough of a GitHub error body to recognise the failure by
    private const int MaxErrorDetailsLength = 500;

    public virtual async Task<GitHubRepositoryInfo> GetRepositoryAsync(string repository, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var operation = $"read repository \"{repository}\"";
        using var request = new HttpRequestMessage(HttpMethod.Get, GetRepositoryPath(repository));
        using var response = await SendAsync(request, operation, cancellationToken);

        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await ReadAsAsync<GitHubRepositoryInfo>(response, operation, cancellationToken);
    }

    public virtual async Task<GitHubRepositoryInfo> CreateRepositoryAsync(string repository, JObject settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var (organization, _) = ParseRepository(repository);
        var operation = $"create repository \"{repository}\"";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"orgs/{Uri.EscapeDataString(organization)}/repos")
        {
            Content = CreateJsonContent(settings),
        };
        using var response = await SendAsync(request, operation, cancellationToken);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            return details.Contains(AlreadyExistsError, StringComparison.OrdinalIgnoreCase)
                ? null
                : throw CreateException(response, details, operation);
        }

        return await ReadAsAsync<GitHubRepositoryInfo>(response, operation, cancellationToken);
    }

    public virtual async Task UpdateRepositoryAsync(string repository, JObject settings, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentNullException.ThrowIfNull(settings);

        using var request = new HttpRequestMessage(HttpMethod.Patch, GetRepositoryPath(repository)) { Content = CreateJsonContent(settings) };
        using var response = await SendAsync(request, $"update repository \"{repository}\"", cancellationToken);
        await EnsureSuccessAsync(response, $"update repository \"{repository}\"", cancellationToken);
    }

    public virtual async Task<bool> HasOrganizationAsync(string organization, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);

        var operation = $"read organization \"{organization}\"";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"orgs/{Uri.EscapeDataString(organization.Trim())}");
        using var response = await SendAsync(request, operation, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, operation, cancellationToken);
        return true;
    }

    public virtual async Task<JObject> PostGraphQlAsync(Uri url, string owner, JObject request, string operation, CancellationToken cancellationToken = default)
    {
        using var message = CreateGraphQlRequest(url, owner, request);
        using var response = await SendAsync(message, operation, cancellationToken);
        var body = await ReadAsAsync<JObject>(response, operation, cancellationToken);

        // GraphQL reports failures inside a successful response; a repository that does not exist is an answer: data.repository is null
        if (body?["errors"] is JArray { Count: > 0 } errors && !errors.All(IsRepositoryNotFound))
        {
            throw new GitContentException($"GitHub request to {operation} failed: {errors[0]?["message"]}");
        }

        return body;
    }

    private static bool IsRepositoryNotFound(JToken error) =>
        error.Value<string>("type") == GraphQlNotFoundError && error["path"] is JArray { Count: 1 } path && path[0].Value<string>() == GraphQlRepositoryField;

    public virtual async Task<string> GetCommitTreeAsync(string repository, string commitSha, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);

        var operation = $"read commit {commitSha} of \"{repository}\"";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GetRepositoryPath(repository)}/git/commits/{Uri.EscapeDataString(commitSha)}");
        using var response = await SendAsync(request, operation, cancellationToken);
        var body = await ReadAsAsync<JObject>(response, operation, cancellationToken);

        return body?.SelectToken("tree.sha")?.Value<string>() ?? throw new GitContentException($"GitHub request to {operation} returned no tree.");
    }

    public virtual async Task<string> CreateTreeAsync(string repository, string baseTreeSha, IDictionary<string, string> files, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseTreeSha);
        ArgumentNullException.ThrowIfNull(files);

        var body = new JObject
        {
            ["base_tree"] = baseTreeSha,
            ["tree"] = new JArray(files.Select(x => new JObject
            {
                ["path"] = x.Key,
                ["mode"] = BlobMode,
                ["type"] = BlobType,
                ["content"] = x.Value,
            })),
        };

        var operation = $"create a tree in \"{repository}\"";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetRepositoryPath(repository)}/git/trees") { Content = CreateJsonContent(body) };
        using var response = await SendAsync(request, operation, cancellationToken);

        return await ReadShaAsync(response, operation, cancellationToken);
    }

    public virtual async Task<string> CreateCommitAsync(string repository, string treeSha, string parentSha, string message, GitCommitAuthor author,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(treeSha);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentSha);

        var body = new JObject
        {
            ["message"] = message,
            ["tree"] = treeSha,
            ["parents"] = new JArray(parentSha),
        };
        if (!string.IsNullOrEmpty(author?.Name) && !string.IsNullOrEmpty(author.Email))
        {
            body["author"] = new JObject { ["name"] = author.Name, ["email"] = author.Email };
        }

        var operation = $"create a commit in \"{repository}\"";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetRepositoryPath(repository)}/git/commits") { Content = CreateJsonContent(body) };
        using var response = await SendAsync(request, operation, cancellationToken);

        return await ReadShaAsync(response, operation, cancellationToken);
    }

    public virtual async Task<bool> FastForwardBranchAsync(string repository, string branch, string commitSha, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);

        var body = new JObject
        {
            ["sha"] = commitSha,
            ["force"] = false,
        };

        var operation = $"move branch \"{branch}\" of \"{repository}\" to {commitSha}";
        var url = $"{GetRepositoryPath(repository)}/git/refs/heads/{string.Join("/", branch.Split('/').Select(Uri.EscapeDataString))}";
        using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = CreateJsonContent(body) };
        using var response = await SendAsync(request, operation, cancellationToken);
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return false;
        }

        await EnsureSuccessAsync(response, operation, cancellationToken);
        return true;
    }

    public virtual Task<HttpResponseMessage> SendAnonymousAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken = default) =>
        SendRequestAsync(httpClientFactory.CreateClient(AnonymousHttpClientName), request, operation, cancellationToken);

    protected virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken) =>
        SendRequestAsync(httpClientFactory.CreateClient(HttpClientName), request, operation, cancellationToken);

    public static void ConfigureHttpClient(HttpClient httpClient, GitContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        // relative request uris are combined against BaseAddress, so it must end with '/'
        httpClient.BaseAddress = new Uri(options.ApiUrl.AbsoluteUri.TrimEnd('/') + "/");
        httpClient.Timeout = options.RequestTimeout;
        AddDefaultHeaders(httpClient.DefaultRequestHeaders);
    }

    public static void AddDefaultHeaders(HttpRequestHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        headers.Accept.ParseAdd(MediaType);
        headers.Add(ApiVersionHeaderName, ApiVersion);
        // GitHub rejects requests without a User-Agent
        headers.UserAgent.ParseAdd(UserAgent);
    }

    public static async Task<HttpResponseMessage> SendRequestAsync(HttpClient client, HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        try
        {
            return await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new GitContentException($"GitHub request to {operation} failed: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitContentException($"GitHub request to {operation} failed: {ex.Message}", ex);
        }
    }

    public static async Task<HttpResponseMessage> SendRequestAsync(HttpClient client, HttpMethod method, string url, HttpContent content, string operation,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        return await SendRequestAsync(client, request, operation, cancellationToken);
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateException(response, await response.Content.ReadAsStringAsync(cancellationToken), operation);
        }
    }

    public static async Task<T> ReadAsAsync<T>(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateException(response, body, operation);
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(body);
        }
        catch (JsonException ex)
        {
            throw new GitContentException($"GitHub request to {operation} returned malformed JSON: {ex.Message}", ex);
        }
    }

    public static GitContentException CreateException(HttpResponseMessage response, string details, string operation)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (details?.Length > MaxErrorDetailsLength)
        {
            details = details[..MaxErrorDetailsLength];
        }

        return new GitContentException($"GitHub request to {operation} failed: {(int)response.StatusCode} {response.ReasonPhrase}. {details}");
    }

    public static StringContent CreateJsonContent(JObject body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return new StringContent(body.ToString(), Encoding.UTF8, JsonMediaType);
    }

    public static HttpRequestMessage CreateGraphQlRequest(Uri url, string owner, JObject body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = CreateJsonContent(body) };
        // GraphQL urls name no owner: the auth handler needs it to pick the installation
        request.Options.Set(GitHubAuthHandler.OwnerOptionKey, owner);
        return request;
    }

    public static (string Owner, string Name) ParseRepository(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var separator = repository.IndexOf('/');
        if (separator <= 0 || separator == repository.Length - 1)
        {
            throw new ArgumentException($"\"{repository}\" is not a repository in \"owner/name\" form.", nameof(repository));
        }

        return (repository[..separator], repository[(separator + 1)..]);
    }

    /// <summary>Reads a GitHub timestamp culture-invariantly; Newtonsoft may already have parsed it into a date.</summary>
    public static DateTime? ReadDate(JToken value) => value switch
    {
        JValue { Value: DateTime dateTime } => dateTime.ToUniversalTime(),
        JValue { Value: DateTimeOffset offset } => offset.UtcDateTime,
        JValue { Value: string text } when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed.UtcDateTime,
        _ => null,
    };

    private static string GetRepositoryPath(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        return $"repos/{repository}";
    }

    private static async Task<string> ReadShaAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var body = await ReadAsAsync<JObject>(response, operation, cancellationToken);

        return body?["sha"]?.Value<string>() ?? throw new GitContentException($"GitHub request to {operation} returned no sha.");
    }
}
