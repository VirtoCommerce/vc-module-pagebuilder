namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>How a store came by its content repository, which decides what the platform keeps in it.</summary>
public enum GitRepositorySource
{
    /// <summary>
    /// The store's own repository in the organization, "{Organization}/{RepositoryPrefix}{storeId}", which the
    /// module creates for it: the platform records the history of the store's database pages and theme files there,
    /// and writes the live page itself once a publication is merged.
    /// </summary>
    Site,

    /// <summary>
    /// The repository the store's "Git repository" setting names, assigned by hand: used as it is, with the git flow
    /// of earlier installations — its own deployment, no history of database pages or theme files.
    /// </summary>
    StoreSetting,

    /// <summary>The single <see cref="GitContentOptions.Repository"/> every store shared before: as <see cref="StoreSetting"/>.</summary>
    Shared,
}

/// <summary>A store's content repository ("owner/name") and how the store came by it.</summary>
public sealed record StoreRepository(string Name, GitRepositorySource Source);
