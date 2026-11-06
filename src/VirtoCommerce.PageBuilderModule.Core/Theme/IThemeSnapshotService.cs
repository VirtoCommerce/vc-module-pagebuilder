namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeSnapshotService
{
    /// <summary>Gets the published theme snapshot for the culture; null when the new Page Builder is off for the store.</summary>
    Task<ThemeSnapshot> GetPublishedAsync(string storeId, string cultureName, CancellationToken cancellationToken = default);

    /// <summary>Whether the store has the new Page Builder on: its theme reaches the storefront as published snapshots.</summary>
    Task<bool> IsNewPageBuilderEnabledAsync(string storeId);
}
