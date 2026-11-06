using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core;
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
    IPageBuilderPageService pageService,
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

    public virtual Task RecordRestoreAsync(GroupedPageBuilderPage group, string content, string sha, string version, ClaimsPrincipal user, string saveId,
        CancellationToken cancellationToken = default)
    {
        return RecordAsync(group, content, GetRestoredSummary(version, sha), user, saveId, cancellationToken);
    }

    public virtual async Task<GitFileHistoryResult> GetHistoryAsync(GroupedPageBuilderPage group, string after = null, int take = ModuleConstants.GitHistory.DefaultPageSize,
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

    public virtual async Task<bool> DiscardDraftAsync(GroupedPageBuilderPage group, ClaimsPrincipal user, string saveId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (!group.Pages.Any(x => x.Status == Published))
        {
            return false;
        }

        var draftIds = group.Pages.Where(x => x.Status == Draft).Select(x => x.Id).ToList();
        if (draftIds.Count > 0)
        {
            await pageService.DeleteAsync(draftIds);
            await RecordAsync(group, await GetPublishedContentAsync(group, cancellationToken), ModuleConstants.GitHistory.DiscardedSummary, user, saveId, cancellationToken);
        }

        return true;
    }

    protected virtual string GetFilePath(string groupId) => $"{Folder}/{groupId}.json";

    protected virtual string GetCommitSubject(GroupedPageBuilderPage group) => $": \"{group.Name}\" ({group.Permalink})";
}
