namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public sealed record StoreRepository(string Name, GitRepositorySource Source)
{
    /// <summary>Created by the module: the platform writes its live pages and keeps file history in it.</summary>
    public bool IsPlatformManaged => Source == GitRepositorySource.Site;
}
