namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderLinkedComponentContentService
{
    Task<string> LoadContentAsync(string linkedComponentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
        IEnumerable<string> linkedComponentIds,
        CancellationToken cancellationToken = default);

    Task SaveContentAsync(
        string linkedComponentId,
        string content,
        CancellationToken cancellationToken = default);
}
