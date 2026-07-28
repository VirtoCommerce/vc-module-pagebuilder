namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderLinkedComponentReferenceIndexService
{
    Task ValidatePageReferencesAsync(string pageId, string content, CancellationToken cancellationToken = default);

    Task PreparePageIndexAsync(string pageId, string content, CancellationToken cancellationToken = default);

    Task RebuildPageIndexAsync(string pageId, string content, CancellationToken cancellationToken = default);

    Task<IList<string>> GetPageIdsAsync(
        IEnumerable<string> linkedComponentIds,
        CancellationToken cancellationToken = default);

    Task TouchPagesAsync(
        IEnumerable<string> pageIds,
        CancellationToken cancellationToken = default);
}
