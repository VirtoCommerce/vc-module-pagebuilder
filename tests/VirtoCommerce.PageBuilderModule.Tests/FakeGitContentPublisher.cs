using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class FakeGitContentPublisher(GitPublishState state, GitPendingPublish openPullRequest = null) : IGitContentPublisher
{
    public List<(string Branch, string Into)> Merges { get; } = [];

    public string PullRequestUrl { get; init; }

    public Action OnMerge { get; set; }

    public Task<GitPublishResult> MergeBranchAsync(string repository, string branch, string title, CancellationToken cancellationToken = default) =>
        MergeBranchIntoAsync(repository, branch, title, "master", cancellationToken);

    public Task<GitPublishResult> MergeBranchIntoAsync(string repository, string branch, string title, string baseBranch, CancellationToken cancellationToken = default)
    {
        Merges.Add((branch, baseBranch));
        OnMerge?.Invoke();
        return Task.FromResult(new GitPublishResult { State = state, PullRequestNumber = 7, Url = PullRequestUrl });
    }

    public Task<GitPendingPublish> GetOpenPullRequestAsync(string repository, string branch, CancellationToken cancellationToken = default) =>
        Task.FromResult(openPullRequest);
}
