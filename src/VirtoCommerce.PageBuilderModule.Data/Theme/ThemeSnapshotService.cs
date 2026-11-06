using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemeSnapshotService(
    IStoreService storeService,
    ISettingsManager settingsManager,
    IThemeStorageFactory themeStorageFactory,
    IThemePublisher themePublisher,
    IPlatformMemoryCache platformMemoryCache,
    IOptions<ThemeSnapshotOptions> options) : IThemeSnapshotService
{
    private const string MenusProperty = "menus";

    public virtual async Task<ThemeSnapshot> GetPublishedAsync(string storeId, string cultureName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(storeId) || !await IsNewPageBuilderEnabledAsync(storeId))
        {
            return null;
        }

        var store = await storeService.GetNoCloneAsync(storeId);
        if (store == null)
        {
            return null;
        }

        var languages = store.Languages?.ToList() ?? [];
        var defaultLanguage = store.GetThemeDefaultLanguage();
        var language = ResolveLanguage(languages, defaultLanguage, cultureName);

        var cacheKey = CacheKey.With(GetType(), nameof(GetPublishedAsync), store.Id, language);
        return await platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async cacheOptions =>
        {
            ConfigureCache(cacheOptions, store.Id);
            return await GetPublishedNoCacheAsync(store.Id, language, defaultLanguage, cancellationToken);
        });
    }

    public virtual async Task<bool> IsNewPageBuilderEnabledAsync(string storeId)
    {
        var descriptor = ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder;
        var settings = await settingsManager.GetObjectSettingsAsync([descriptor.Name], ModuleConstants.Settings.StoreSettingsObjectType, storeId);

        return settings.GetValue<bool>(descriptor);
    }

    protected virtual string ResolveLanguage(IList<string> languages, string defaultLanguage, string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return defaultLanguage;
        }

        var exactLanguage = languages.FirstOrDefault(x => string.Equals(x, cultureName, StringComparison.OrdinalIgnoreCase));
        if (exactLanguage != null)
        {
            return exactLanguage;
        }

        static string GetLanguageCode(string name) => name.Split('-')[0];

        var languageCode = GetLanguageCode(cultureName);
        var sameLanguages = languages.Where(x => string.Equals(GetLanguageCode(x), languageCode, StringComparison.OrdinalIgnoreCase)).ToList();

        return sameLanguages.FirstOrDefault(x => string.Equals(x, defaultLanguage, StringComparison.OrdinalIgnoreCase))
            ?? sameLanguages.FirstOrDefault()
            ?? defaultLanguage;
    }

    protected virtual async Task<ThemeSnapshot> GetPublishedNoCacheAsync(string storeId, string language, string defaultLanguage, CancellationToken cancellationToken)
    {
        var storage = await themeStorageFactory.OpenAsync(storeId);
        var publication = await storage.GetCurrentPublicationAsync() ?? await PublishFirstAsync(storeId, cancellationToken);

        // A language added after the publication gets the default language snapshot.
        foreach (var servedLanguage in new[] { language, defaultLanguage }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await storage.ReadSnapshotAsync(publication, servedLanguage) is { } content)
            {
                return CreateSnapshot(storeId, servedLanguage, publication, content);
            }
        }

        // The store default language may have changed since the publication.
        if (await storage.GetPublicationRecordAsync(publication) is { } record
            && await storage.ReadSnapshotAsync(publication, record.DefaultLanguage) is { } defaultContent)
        {
            return CreateSnapshot(storeId, record.DefaultLanguage, publication, defaultContent);
        }

        throw new InvalidOperationException(
            $"The theme publication {publication.ToString(CultureInfo.InvariantCulture)} of store '{storeId}' has no snapshot in {language}.");
    }

    protected virtual async Task<int> PublishFirstAsync(string storeId, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey.With(GetType(), nameof(PublishFirstAsync), storeId);
        if (platformMemoryCache.TryGetValue(cacheKey, out IReadOnlyList<string> errors))
        {
            throw new ThemeValidationException(errors);
        }

        var cacheOptions = platformMemoryCache.GetDefaultCacheEntryOptions();
        ConfigureCache(cacheOptions, storeId);
        try
        {
            return await themePublisher.PublishFirstAsync(storeId, cancellationToken);
        }
        catch (ThemeValidationException exception)
        {
            // Cache the failure until the next publication, so reads do not retry under the theme lock.
            platformMemoryCache.Set(cacheKey, exception.Errors, cacheOptions);
            throw;
        }
    }

    protected virtual void ConfigureCache(MemoryCacheEntryOptions cacheOptions, string storeId)
    {
        // Absolute expiration: without Redis, other nodes do not receive the publication's cache reset.
        var lifetime = options.Value.CacheAbsoluteExpiration;
        cacheOptions.SlidingExpiration = null;
        if (cacheOptions.AbsoluteExpirationRelativeToNow is not { } configured || configured > lifetime)
        {
            cacheOptions.AbsoluteExpirationRelativeToNow = lifetime;
        }

        cacheOptions.AddExpirationToken(ThemeSnapshotCacheRegion.CreateChangeTokenForKey(storeId));
    }

    protected virtual ThemeSnapshot CreateSnapshot(string storeId, string cultureName, int publication, string content)
    {
        return new ThemeSnapshot(storeId, cultureName, publication, content, GetMenus(content));
    }

    protected virtual IReadOnlyList<string> GetMenus(string content)
    {
        try
        {
            return (JObject.Parse(content)[MenusProperty] as JArray)?.Values<string>().Where(x => !string.IsNullOrEmpty(x)).ToList() ?? [];
        }
        catch (JsonReaderException)
        {
            return [];
        }
    }
}
