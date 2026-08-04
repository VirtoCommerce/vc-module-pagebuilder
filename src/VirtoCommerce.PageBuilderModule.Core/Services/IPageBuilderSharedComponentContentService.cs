using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentContentService
{
    Task<string> LoadContentAsync(string sharedComponentId, CancellationToken cancellationToken = default);

    Task<string> TryLoadContentAsync(
        PageBuilderSharedComponent expectedComponent,
        CancellationToken cancellationToken = default)
    {
        // Keep custom implementations compatible and fail closed until they opt in.
        return Task.FromResult<string>(null);
    }

    Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
        IEnumerable<string> sharedComponentIds,
        CancellationToken cancellationToken = default);

    Task SaveContentAsync(
        string sharedComponentId,
        string content,
        CancellationToken cancellationToken = default);

    Task<bool> TrySaveContentAsync(
        PageBuilderSharedComponent expectedComponent,
        string content,
        CancellationToken cancellationToken = default)
    {
        // Keep custom implementations compatible and fail closed until they opt in.
        return Task.FromResult(false);
    }
}
