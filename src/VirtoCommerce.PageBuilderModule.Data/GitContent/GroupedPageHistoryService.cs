using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent;

/// <summary>Version history of a database page in the store repository.</summary>
public class GroupedPageHistoryService(
    IGitContentPolicy gitContentPolicy,
    IGitContentRepository gitContentRepository,
    IGitHubApiClient gitHubApiClient,
    IGitHubConnectionService gitHubConnectionService,
    IGroupedPageService groupedPageService,
    IOptions<GitContentOptions> options,
    ILogger<GroupedPageHistoryService> logger)
    : GitHubFileHistoryService(gitContentPolicy, gitContentRepository, gitHubApiClient, gitHubConnectionService, options, logger), IGroupedPageHistoryService
{
    public const string Folder = "database-pages";

    public virtual Task RecordAsync(GroupedPageBuilderPage group, string content, string summary, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        return RecordFileAsync(group.StoreId, GetFilePath(group.Id), GetCommitSubject(group), content, summary, user, saveId, cancellationToken);
    }

    public virtual async Task<GitFileHistoryResult> GetHistoryAsync(GroupedPageBuilderPage group, string after = null, int take = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var publishedContent = await GetPublishedContentAsync(group, cancellationToken);
        return await GetFileHistoryAsync(group.StoreId, GetFilePath(group.Id), GetCommitSubject(group), publishedContent, after, take, cancellationToken);
    }

    public virtual Task<string> ReadContentAsync(GroupedPageBuilderPage group, string sha, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        return ReadFileAsync(group.StoreId, GetFilePath(group.Id), sha, cancellationToken);
    }

    public virtual async Task<string> GetPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var page = PageBuilderPageSelection.Order(group.Pages ?? []).FirstOrDefault(x => x.Status == Published);
        return page == null ? null : await groupedPageService.LoadContent(page.Id, cancellationToken);
    }

    protected virtual string GetFilePath(string groupId) => $"{Folder}/{groupId}.json";

    // the repository holds every page of the site
    protected virtual string GetCommitSubject(GroupedPageBuilderPage group) => $": \"{group.Name}\" ({group.Permalink})";
}
