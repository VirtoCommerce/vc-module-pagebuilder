namespace VirtoCommerce.PageBuilderModule.Data.BackgroundJobs;

public enum GitContentRepositoryAction
{
    /// <summary>Ensures the store repository and enables git content for the store.</summary>
    Connect,

    /// <summary>Archives the repository of a deleted store.</summary>
    Archive,
}
