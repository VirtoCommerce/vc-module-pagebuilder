using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class TestGroupedPageHistoryService(
    IGitContentPolicy policy,
    IGitContentRepository repository,
    GitHubScript github,
    IOptions<GitContentOptions> options,
    string publishedContent = null,
    IPageBuilderPageService pageService = null)
    : GroupedPageHistoryService(policy, repository, new GitHubApiClient(github), GitHubConnections.CreateService(options.Value), null, pageService, options,
        NullLogger<GroupedPageHistoryService>.Instance)
{
    public override Task<string> GetPublishedContentAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default) =>
        Task.FromResult(publishedContent);
}
