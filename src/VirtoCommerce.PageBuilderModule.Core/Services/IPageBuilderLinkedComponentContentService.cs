using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderLinkedComponentContentService
{
    Task<string> LoadContentAsync(string linkedComponentId, CancellationToken cancellationToken = default);

    Task<string> TryLoadContentAsync(
        PageBuilderLinkedComponent expectedComponent,
        CancellationToken cancellationToken = default)
    {
        // Keep custom implementations compatible and fail closed until they opt in.
        return Task.FromResult<string>(null);
    }

    Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
        IEnumerable<string> linkedComponentIds,
        CancellationToken cancellationToken = default);

    Task SaveContentAsync(
        string linkedComponentId,
        string content,
        CancellationToken cancellationToken = default);

    Task<bool> TrySaveContentAsync(
        PageBuilderLinkedComponent expectedComponent,
        string content,
        CancellationToken cancellationToken = default)
    {
        // Keep custom implementations compatible and fail closed until they opt in.
        return Task.FromResult(false);
    }
}
