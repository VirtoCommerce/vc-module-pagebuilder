namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

internal static class PageBuilderRepositoryCapabilities
{
    internal static IPageBuilderLinkedComponentRepository RequireLinkedComponents(
        this IPageBuilderModuleRepository repository)
    {
        return repository as IPageBuilderLinkedComponentRepository
            ?? throw new NotSupportedException("Shared Components require linked-component repository support.");
    }
}
