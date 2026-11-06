namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Source of a store content repository.</summary>
public enum GitRepositorySource
{
    /// <summary>Created by the module in the organization; keeps database page and theme file history.</summary>
    Site,

    /// <summary>Named in the store setting; used as is.</summary>
    StoreSetting,

    /// <summary>The shared <see cref="GitContentOptions.Repository"/>.</summary>
    Shared,
}
