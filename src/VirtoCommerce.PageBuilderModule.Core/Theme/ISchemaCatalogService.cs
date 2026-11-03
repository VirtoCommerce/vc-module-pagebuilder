namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>
/// The storefront schemas the designer previewed, kept by their hash: a publication is validated against the catalog
/// of the storefront its editor saw.
/// </summary>
public interface ISchemaCatalogService
{
    /// <summary>Keeps the catalog under its <c>hash</c> unless one is kept already, and makes it the store's current one.</summary>
    /// <returns>The hash.</returns>
    Task<string> SaveAsync(string storeId, string catalogJson, CancellationToken cancellationToken = default);

    /// <summary>The catalog with that hash, the store's current one when <paramref name="hash"/> is empty; <c>null</c> when there is none.</summary>
    Task<string> GetAsync(string storeId, string hash, CancellationToken cancellationToken = default);
}
