namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>Storefront schema catalogs a theme publication is validated against.</summary>
public interface ISchemaCatalogService
{
    /// <summary>Saves the catalog and makes it the store's current one. Returns the catalog hash.</summary>
    Task<string> SaveAsync(string storeId, string catalogJson, CancellationToken cancellationToken = default);

    /// <summary>Gets the catalog by hash, or the current catalog when the hash is empty.</summary>
    Task<string> GetAsync(string storeId, string hash, CancellationToken cancellationToken = default);
}
