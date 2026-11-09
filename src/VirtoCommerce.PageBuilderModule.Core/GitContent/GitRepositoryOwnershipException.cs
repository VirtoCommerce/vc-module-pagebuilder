using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public sealed class GitRepositoryOwnershipException(string repository, string owner)
    : InvalidOperationException($"The repository \"{repository}\" belongs to store \"{owner}\". Set this store's Git repository to one of its own.")
{
    public string Repository { get; } = repository;

    public string Owner { get; } = owner;
}
