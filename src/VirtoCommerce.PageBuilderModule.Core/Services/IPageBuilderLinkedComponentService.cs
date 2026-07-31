using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderLinkedComponentService : ICrudService<PageBuilderLinkedComponent>
{
    Task<PageBuilderLinkedComponent> UpdateMetadataAsync(
        PageBuilderLinkedComponent model,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<PageBuilderLinkedComponent>(null);
    }

    Task<bool> TryDeleteAsync(
        PageBuilderLinkedComponent expectedComponent,
        CancellationToken cancellationToken = default)
    {
        // Keep custom implementations compatible and fail closed until they opt in.
        return Task.FromResult(false);
    }

    Task SaveWithContentAsync(
        PageBuilderLinkedComponent model,
        string content,
        CancellationToken cancellationToken = default);
}
