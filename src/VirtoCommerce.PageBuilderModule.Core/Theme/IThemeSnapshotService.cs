namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeSnapshotService
{
    /// <summary>Gets the published theme snapshot for the culture; null when the new Page Builder is off for the store.</summary>
    Task<ThemeSnapshot> GetPublishedAsync(string storeId, string cultureName, CancellationToken cancellationToken = default);
}
