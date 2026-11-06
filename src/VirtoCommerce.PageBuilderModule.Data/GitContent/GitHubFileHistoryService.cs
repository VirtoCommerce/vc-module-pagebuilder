using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>Version history of a site file in the store repository. Recording is best effort.</summary>
public abstract class GitHubFileHistoryService(
    IGitContentPolicy gitContentPolicy,
    IGitContentRepository gitContentRepository,
    IGitHubApiClient gitHubApiClient,
    IGitHubConnectionService gitHubConnectionService,
    IOptions<GitContentOptions> options,
    ILogger logger)
{
    // GitHub's limit on a GraphQL page
    private const int MaxPageSize = 100;

    private const string HistoryQuery = """
        query($owner:String!,$name:String!,$branch:String!,$path:String!,$take:Int!,$after:String){
          repository(owner:$owner,name:$name){
            ref(qualifiedName:$branch){
              target{ ... on Commit{
                history(path:$path, first:$take, after:$after){
                  pageInfo{ hasNextPage endCursor }
                  nodes{ oid committedDate messageHeadline messageBody author{ name email user{ login } } file(path:$path){ oid } }
                }
              } }
            }
          }
        }
        """;

    /// <summary>Computes the git blob id of the content.</summary>
    public static string ComputeBlobOid(string content)
    {
        if (content == null)
        {
            return null;
        }

        var bytes = PageJson.Encoding.GetBytes(content);
        var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
#pragma warning disable CA5350 // git names blobs by SHA-1; this is an identity, not a security check
        return Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes]));
#pragma warning restore CA5350
    }

    /// <summary>Commits the file content to the base branch unless it is unchanged.</summary>
    protected virtual async Task RecordFileAsync(string storeId, string path, string commitSubject, string content, string summary, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken)
    {
        try
        {
            var repository = await GetHistoryRepositoryAsync(storeId, cancellationToken);
            if (repository == null || content == null)
            {
                return;
            }

            var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
            var message = GitCommitMessage.WithTrailer(
                GitCommitMessage.WithVcUser($"{summary}{commitSubject}", user?.Identity?.Name),
                GitCommitMessage.VcSaveTrailer,
                saveId);

            await gitContentRepository.CommitFileAsync(repository, path, content, connection.BaseBranch, message, CreateAuthor(user), cancellationToken);
        }
        // best effort: a failed commit must not fail the save
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Version history of {Path} (store {StoreId}) missed a change: {Summary}.", path, storeId, summary);
        }
    }

    /// <summary>Gets the file changes, newest first; <c>null</c> when the store keeps no history.</summary>
    protected virtual async Task<GitFileHistoryResult> GetFileHistoryAsync(string storeId, string path, string commitSubject, string publishedContent, string after, int take,
        CancellationToken cancellationToken)
    {
        var repository = await GetHistoryRepositoryAsync(storeId, cancellationToken);
        if (repository == null)
        {
            return null;
        }

        var connection = await gitHubConnectionService.GetConnectionAsync(cancellationToken);
        var (owner, name) = GitHubApiClient.ParseRepository(repository);
        var request = new JObject
        {
            ["query"] = HistoryQuery,
            ["variables"] = new JObject
            {
                ["owner"] = owner,
                ["name"] = name,
                ["branch"] = $"refs/heads/{connection.BaseBranch}",
                ["path"] = path,
                ["take"] = Math.Clamp(take, 1, MaxPageSize),
                ["after"] = after,
            },
        };

        var body = await gitHubApiClient.PostGraphQlAsync(options.Value.GraphQlUrl, owner, request, $"read the version history of \"{path}\"", cancellationToken);
        var history = body?.SelectToken("data.repository.ref.target.history");

        var result = AbstractTypeFactory<GitFileHistoryResult>.TryCreateInstance();
        result.Versions = GetVersions(history?["nodes"] as JArray ?? [], commitSubject, ComputeBlobOid(publishedContent));
        result.HasMore = history?.SelectToken("pageInfo.hasNextPage")?.Value<bool>() == true;
        result.EndCursor = history?.SelectToken("pageInfo.endCursor")?.Value<string>();

        return result;
    }

    /// <summary>Gets the file content at a change; <c>null</c> when there is no such change or no history.</summary>
    protected virtual async Task<string> ReadFileAsync(string storeId, string path, string sha, CancellationToken cancellationToken)
    {
        var repository = await GetHistoryRepositoryAsync(storeId, cancellationToken);
        return repository == null ? null : await gitContentRepository.ReadFileAsync(repository, path, sha, cancellationToken);
    }

    /// <summary>Gets the store repository when it keeps file history.</summary>
    protected virtual async Task<string> GetHistoryRepositoryAsync(string storeId, CancellationToken cancellationToken)
    {
        if (await gitContentPolicy.GetConfiguredRepositoryAsync(storeId, cancellationToken) is not { Source: GitRepositorySource.Site })
        {
            return null;
        }

        return await gitContentPolicy.GetRepositoryAsync(storeId, cancellationToken);
    }

    protected virtual GitCommitAuthor CreateAuthor(ClaimsPrincipal user)
    {
        var userName = user?.Identity?.Name;

        return new GitCommitAuthor
        {
            Name = string.IsNullOrEmpty(userName) ? options.Value.FallbackAuthorName : userName,
            Email = user?.FindFirstValue(ClaimTypes.Email) ?? options.Value.FallbackAuthorEmail,
        };
    }

    protected virtual IList<GitFileVersion> GetVersions(JArray nodes, string commitSubject, string publishedOid)
    {
        var versions = new List<GitFileVersion>();
        var published = false;
        foreach (var node in nodes)
        {
            // only the newest change with the published content is live
            var isPublished = !published && publishedOid != null &&
                              string.Equals(node.SelectToken("file.oid")?.Value<string>(), publishedOid, StringComparison.Ordinal);
            published |= isPublished;

            var headline = node["messageHeadline"]?.Value<string>();
            var messageBody = node["messageBody"]?.Value<string>();

            var version = AbstractTypeFactory<GitFileVersion>.TryCreateInstance();
            version.Sha = node["oid"]?.Value<string>();
            version.Date = GitHubApiClient.ReadDate(node["committedDate"]);
            // drop the file suffix from the headline
            version.Message = headline != null && headline.EndsWith(commitSubject, StringComparison.Ordinal) ? headline[..^commitSubject.Length] : headline;
            version.VcUser = GitCommitMessage.ReadTrailer(messageBody, GitCommitMessage.VcUserTrailer);
            version.SaveId = GitCommitMessage.ReadTrailer(messageBody, GitCommitMessage.VcSaveTrailer);
            version.AuthorName = node.SelectToken("author.name")?.Value<string>();
            version.AuthorEmail = node.SelectToken("author.email")?.Value<string>();
            version.AuthorLogin = node.SelectToken("author.user.login")?.Value<string>();
            version.Published = isPublished;
            versions.Add(version);
        }

        return versions;
    }
}
