namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

internal static class PageBuilderRepositoryCapabilities
{
    internal static IPageBuilderSharedComponentRepository RequireSharedComponents(
        this IPageBuilderModuleRepository repository)
    {
        return repository as IPageBuilderSharedComponentRepository
            ?? throw new NotSupportedException("Shared Components require shared-component repository support.");
    }
}
