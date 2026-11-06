namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>A store content repository ("owner/name") and its source.</summary>
public sealed record StoreRepository(string Name, GitRepositorySource Source);
