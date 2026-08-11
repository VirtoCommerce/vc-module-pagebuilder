namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentReferenceIndexService
{
    Task ValidateReferencesForStoreAsync(
        string storeId,
        IEnumerable<string> contents,
        CancellationToken cancellationToken = default);

    Task<IList<string>> GetPageIdsAsync(
        IEnumerable<string> sharedComponentIds,
        CancellationToken cancellationToken = default);

}
