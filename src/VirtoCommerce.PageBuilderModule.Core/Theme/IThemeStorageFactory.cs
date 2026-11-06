namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeStorageFactory
{
    /// <summary>Opens the active theme of the store.</summary>
    Task<IThemeStorage> OpenAsync(string storeId);
}
