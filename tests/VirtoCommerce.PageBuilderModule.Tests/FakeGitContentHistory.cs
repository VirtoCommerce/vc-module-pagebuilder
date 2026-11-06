using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class FakeGitContentHistory(GitPageHistory history = null) : IGitContentHistory
{
    public List<string> Invalidated { get; } = [];

    public Task<GitPageHistory> GetHistoryAsync(string repository, string repoPath, GitHistoryQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(history ?? new GitPageHistory());

    public void Invalidate(string repository, string repoPath) => Invalidated.Add(repoPath);
}
