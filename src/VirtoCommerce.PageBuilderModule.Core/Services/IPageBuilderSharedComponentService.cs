using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentService : ICrudService<PageBuilderSharedComponent>
{
    Task<PageBuilderSharedComponent> UpdateMetadataAsync(
        PageBuilderSharedComponent model,
        CancellationToken cancellationToken = default);

    Task<bool> TryDeleteAsync(
        PageBuilderSharedComponent expectedComponent,
        CancellationToken cancellationToken = default);

    Task SaveWithContentAsync(
        PageBuilderSharedComponent model,
        string content,
        CancellationToken cancellationToken = default);
}
