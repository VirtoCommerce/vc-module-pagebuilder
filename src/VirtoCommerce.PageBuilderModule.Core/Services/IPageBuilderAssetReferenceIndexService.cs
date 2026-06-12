namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderAssetReferenceIndexService
{
    Task RebuildPageIndexAsync(string pageId, string content, CancellationToken cancellationToken = default);

    Task DeletePageIndexAsync(IEnumerable<string> pageIds, CancellationToken cancellationToken = default);

    Task DeleteGroupIndexAsync(IEnumerable<string> groupIds, CancellationToken cancellationToken = default);
}
