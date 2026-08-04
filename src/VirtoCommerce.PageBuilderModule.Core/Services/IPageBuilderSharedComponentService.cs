using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentService : ICrudService<PageBuilderSharedComponent>
{
    Task<PageBuilderSharedComponent> UpdateMetadataAsync(
        PageBuilderSharedComponent model,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<PageBuilderSharedComponent>(null);
    }

    Task<bool> TryDeleteAsync(
        PageBuilderSharedComponent expectedComponent,
        CancellationToken cancellationToken = default)
    {
        // Keep custom implementations compatible and fail closed until they opt in.
        return Task.FromResult(false);
    }

    Task SaveWithContentAsync(
        PageBuilderSharedComponent model,
        string content,
        CancellationToken cancellationToken = default);
}
