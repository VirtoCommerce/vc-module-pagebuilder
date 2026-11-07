using VirtoCommerce.ContentModule.Core;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemeStorageFactory(
    IStoreService storeService,
    IContentPathResolver contentPathResolver,
    IBlobContentStorageProviderFactory blobContentStorageProviderFactory,
    IPublishingService publishingService) : IThemeStorageFactory
{
    public virtual async Task<IThemeStorage> OpenAsync(string storeId)
    {
        return Open(storeId, await GetThemeNameAsync(storeId));
    }

    public virtual IThemeStorage Open(string storeId, string themeName)
    {
        var basePath = contentPathResolver.GetContentBasePath(ContentConstants.ContentTypes.Themes, storeId, themeName);

        return CreateStorage(blobContentStorageProviderFactory.CreateProvider(basePath), themeName);
    }

    public virtual async Task<string> GetThemeNameAsync(string storeId)
    {
        var store = await storeService.GetNoCloneAsync(storeId, StoreResponseGroup.DynamicProperties.ToString());
        var themeName = store?.DynamicProperties?
            .FirstOrDefault(x => x.Name == ModuleConstants.Theme.ThemeNamePropertyName)?
            .Values?.FirstOrDefault()?.Value?.ToString();

        return string.IsNullOrEmpty(themeName) ? ModuleConstants.Theme.DefaultThemeName : themeName;
    }

    protected virtual IThemeStorage CreateStorage(IBlobContentStorageProvider provider, string themeName)
    {
        return new ThemeStorage(provider, publishingService, themeName);
    }
}
