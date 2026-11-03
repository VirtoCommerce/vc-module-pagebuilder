namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>The published theme of a store, read anonymously by the storefront.</summary>
public interface IThemeSnapshotService
{
    /// <summary>
    /// The store's current publication in the store language closest to <paramref name="cultureName"/>, else the store
    /// default; <c>null</c> when the store has the New Page Builder setting off. A store with the setting on and no
    /// publication yet gets a first one, built from its live theme files.
    /// </summary>
    Task<ThemeSnapshot> GetPublishedAsync(string storeId, string cultureName, CancellationToken cancellationToken = default);
}
