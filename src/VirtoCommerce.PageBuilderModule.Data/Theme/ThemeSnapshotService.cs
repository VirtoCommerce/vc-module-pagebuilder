using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>
/// The published snapshot a storefront reads (docs/contracts/theme-snapshot.md, Request): the language checked
/// against the store's before the cache, the pointer read only on a cache miss.
/// </summary>
public class ThemeSnapshotService(
    IStoreService storeService,
    ISettingsManager settingsManager,
    ThemeStorageFactory storageFactory,
    ThemePublisher publisher,
    IPlatformMemoryCache platformMemoryCache) : IThemeSnapshotService
{
    private const string FallbackLanguage = "en-US";

    public virtual async Task<ThemeSnapshot> GetPublishedAsync(string storeId, string cultureName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(storeId) || !await IsNewPageBuilderAsync(storeId))
        {
            return null;
        }

        var store = await storeService.GetNoCloneAsync(storeId);
        if (store == null)
        {
            return null;
        }

        var languages = store.Languages?.ToList() ?? [];
        var defaultLanguage = store.DefaultLanguage ?? languages.FirstOrDefault() ?? FallbackLanguage;
        var language = ResolveLanguage(languages, defaultLanguage, cultureName);

        var cacheKey = CacheKey.With(GetType(), nameof(GetPublishedAsync), store.Id, language);
        return await platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async options =>
        {
            options.SlidingExpiration = null;
            options.AbsoluteExpirationRelativeToNow = ModuleConstants.Theme.ThemeSnapshotCacheLifetime;
            options.AddExpirationToken(ThemeSnapshotCacheRegion.CreateChangeTokenForKey(store.Id));
            return await ReadAsync(store.Id, language, defaultLanguage, cancellationToken);
        });
    }

    /// <summary>
    /// The store language closest to <paramref name="requested"/>: equal ignoring case; else one with its language part,
    /// the default when it is one of them; else the default.
    /// </summary>
    public static string ResolveLanguage(IReadOnlyList<string> languages, string defaultLanguage, string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return defaultLanguage;
        }

        var exact = languages.FirstOrDefault(x => string.Equals(x, requested, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        static string LanguagePart(string name) => name.Split('-')[0];
        var part = LanguagePart(requested);
        var sameLanguage = languages.Where(x => string.Equals(LanguagePart(x), part, StringComparison.OrdinalIgnoreCase)).ToList();

        return sameLanguage.FirstOrDefault(x => string.Equals(x, defaultLanguage, StringComparison.OrdinalIgnoreCase))
            ?? sameLanguage.FirstOrDefault()
            ?? defaultLanguage;
    }

    private async Task<ThemeSnapshot> ReadAsync(string storeId, string language, string defaultLanguage, CancellationToken cancellationToken)
    {
        var storage = await storageFactory.OpenAsync(storeId);
        var publication = ThemePublisher.ReadPointer(await storage.ReadAsync(ThemeStorage.Pointer))
            ?? await publisher.PublishFirstAsync(storeId, cancellationToken);

        if (publication < 0)
        {
            // a pointer cut off on a file system: the highest complete publication
            var complete = await ThemePublisher.CompletePublicationsAsync(storage, defaultLanguage);
            publication = complete.Count > 0 ? complete.Max() : await publisher.PublishFirstAsync(storeId, cancellationToken);
        }

        var served = language;
        var content = await storage.ReadAsync(ThemePublisher.SnapshotFile(publication, language));
        if (content == null && !string.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            served = defaultLanguage;
            content = await storage.ReadAsync(ThemePublisher.SnapshotFile(publication, defaultLanguage));
        }

        if (content == null)
        {
            throw new InvalidOperationException(
                $"The theme publication {publication.ToString(CultureInfo.InvariantCulture)} of store '{storeId}' has no snapshot in {served}.");
        }

        return new ThemeSnapshot(storeId, served, publication, content, MenusOf(content));
    }

    private static IReadOnlyList<string> MenusOf(string content)
    {
        try
        {
            return (JObject.Parse(content)["menus"] as JArray)?.Values<string>().Where(x => !string.IsNullOrEmpty(x)).ToList() ?? [];
        }
        catch (JsonReaderException)
        {
            return [];
        }
    }

    private async Task<bool> IsNewPageBuilderAsync(string storeId)
    {
        var setting = ModuleConstants.Settings.StoreLevelSettings.NewPageBuilder;
        var settings = await settingsManager.GetObjectSettingsAsync([setting.Name], ModuleConstants.Settings.StoreSettingsObjectType, storeId);
        return settings?.FirstOrDefault(x => x.Name == setting.Name)?.Value switch
        {
            bool on => on,
            null => false,
            var value => bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed,
        };
    }
}
