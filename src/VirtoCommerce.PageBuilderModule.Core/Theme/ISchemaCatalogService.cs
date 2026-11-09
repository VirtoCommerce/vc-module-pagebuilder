namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface ISchemaCatalogService
{
    /// <summary>Also makes it the store's current catalog. Returns the catalog hash.</summary>
    Task<string> SaveAsync(string storeId, string catalogJson, CancellationToken cancellationToken = default);

    /// <summary>The current catalog when the hash is empty.</summary>
    Task<string> GetAsync(string storeId, string hash, CancellationToken cancellationToken = default);
}
