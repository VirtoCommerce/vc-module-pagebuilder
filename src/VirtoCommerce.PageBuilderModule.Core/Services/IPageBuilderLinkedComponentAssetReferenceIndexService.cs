namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderLinkedComponentAssetReferenceIndexService
{
    Task RebuildIndexAsync(
        string linkedComponentId,
        string content,
        CancellationToken cancellationToken = default);
}
