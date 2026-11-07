using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Thrown when a store repository was created for another store.</summary>
public sealed class GitRepositoryOwnershipException(string repository, string owner)
    : InvalidOperationException($"The repository \"{repository}\" belongs to store \"{owner}\". Set this store's Git repository to one of its own.")
{
    public string Repository { get; } = repository;

    /// <summary>The store the repository was created for.</summary>
    public string Owner { get; } = owner;
}
