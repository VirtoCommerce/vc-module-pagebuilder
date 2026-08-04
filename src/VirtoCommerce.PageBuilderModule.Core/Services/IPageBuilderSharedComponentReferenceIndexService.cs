namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentReferenceIndexService
{
    Task ValidateReferencesForStoreAsync(
        string storeId,
        IEnumerable<string> contents,
        CancellationToken cancellationToken = default);

    Task RebuildPageIndexAsync(string pageId, string content, CancellationToken cancellationToken = default);

    Task<IList<string>> GetPageIdsAsync(
        IEnumerable<string> sharedComponentIds,
        CancellationToken cancellationToken = default);

}
