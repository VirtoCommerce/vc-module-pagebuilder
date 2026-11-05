using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>
/// A store's repository is one the module created for another store: two stores in one repository would publish
/// over each other's pages.
/// </summary>
public sealed class GitRepositoryOwnershipException(string repository, string owner)
    : InvalidOperationException($"The repository \"{repository}\" belongs to store \"{owner}\". Set this store's Git repository to one of its own.")
{
    public string Repository { get; } = repository;

    /// <summary>The store the repository was created for.</summary>
    public string Owner { get; } = owner;
}
