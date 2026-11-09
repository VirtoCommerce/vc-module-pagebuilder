namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemeStorageFactory
{
    Task<IThemeStorage> OpenAsync(string storeId);

    Task<string> GetThemeNameAsync(string storeId);

    IThemeStorage Open(string storeId, string themeName);
}
