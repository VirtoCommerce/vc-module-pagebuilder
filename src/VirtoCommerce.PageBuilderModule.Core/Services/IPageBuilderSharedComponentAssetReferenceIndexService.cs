namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentAssetReferenceIndexService
{
    Task RebuildIndexAsync(
        string sharedComponentId,
        string content,
        CancellationToken cancellationToken = default);
}
