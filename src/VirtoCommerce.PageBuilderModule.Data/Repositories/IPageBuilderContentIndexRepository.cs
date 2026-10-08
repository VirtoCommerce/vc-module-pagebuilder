namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

/// <summary>Rebuilds both reference indexes within the caller's grouped write transaction.</summary>
public interface IPageBuilderContentIndexRepository
{
    Task RebuildPageContentIndexesAsync(string pageId, string content, string storeId, CancellationToken cancellationToken = default);
}
