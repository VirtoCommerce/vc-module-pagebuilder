namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeSnapshotService
{
    /// <summary>
    /// Gets the published theme snapshot for the culture; null when the new Page Builder is off for the store. The first read
    /// publishes the theme's live files when nothing is published yet.
    /// </summary>
    /// <exception cref="ThemeValidationException">Nothing is published yet, and the live files fail the shape check.</exception>
    Task<ThemeSnapshot> GetPublishedAsync(string storeId, string cultureName, CancellationToken cancellationToken = default);

    /// <summary>Checks whether the store serves its theme as published snapshots: the new Page Builder is on.</summary>
    Task<bool> IsNewPageBuilderEnabledAsync(string storeId, CancellationToken cancellationToken = default);
}
