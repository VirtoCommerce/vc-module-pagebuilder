namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeStorageFactory
{
    /// <summary>Opens the active theme of the store.</summary>
    Task<IThemeStorage> OpenAsync(string storeId);

    /// <summary>Opens a theme of the store by its folder name.</summary>
    IThemeStorage Open(string storeId, string themeName);
}
