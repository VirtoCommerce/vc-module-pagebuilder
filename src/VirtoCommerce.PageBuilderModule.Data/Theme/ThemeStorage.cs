using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>The files of a store's active theme, by their path inside the theme folder (docs/contracts/theme-snapshot.md).</summary>
public sealed class ThemeStorage(IBlobContentStorageProvider provider, IPublishingService publishingService, string theme)
{
    public const string TemplatesFolder = "templates";
    public const string Rules = "config/template_rules.json";
    public const string HeaderGroup = "sections/header-group.json";
    public const string FooterGroup = "sections/footer-group.json";
    public const string Brand = "config/brand.json";
    public const string SettingsData = "config/settings_data.json";
    public const string Palette = "config/palette.json";
    public const string PaletteDark = "config/palette.dark.json";
    public const string SnapshotsFolder = "snapshots";
    public const string Pointer = "snapshots/current.json";
    public const string CatalogsFolder = "catalogs";

    private const string FolderType = "folder";

    public string Theme { get; } = theme;

    /// <summary>The path in the theme storage, as the designer's saves and the theme history name it.</summary>
    public string PathOf(string file) => $"{Theme}/{file}";

    public string DraftOf(string file) => publishingService.GetRelativeDraftUrl(file, true);

    /// <summary>The file's text, or <c>null</c> when there is no such file.</summary>
    public async Task<string> ReadAsync(string file)
    {
        var path = PathOf(file);
        if (await provider.GetBlobInfoAsync(path) == null)
        {
            return null;
        }

        await using var stream = await provider.OpenReadAsync(path);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task WriteAsync(string file, string text)
    {
        await using var stream = await provider.OpenWriteAsync(PathOf(file));
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
    }

    public Task RemoveAsync(IEnumerable<string> files) => provider.RemoveAsync(files.Select(PathOf).ToArray());

    /// <summary>The names of the files right in <paramref name="folder"/>.</summary>
    public async Task<IReadOnlyList<string>> ListFilesAsync(string folder) =>
        (await provider.SearchAsync(PathOf(folder), null)).Results.Where(x => x.Type != FolderType).Select(x => x.Name).ToList();

    /// <summary>The names of the folders right in <paramref name="folder"/>.</summary>
    public async Task<IReadOnlyList<string>> ListFoldersAsync(string folder) =>
        (await provider.SearchAsync(PathOf(folder), null)).Results.Where(x => x.Type == FolderType).Select(x => x.Name).ToList();
}

/// <summary>Opens the active theme of a store: the one its <c>DefaultThemeName</c> property names, else <c>default</c>.</summary>
public class ThemeStorageFactory(
    IStoreService storeService,
    IContentPathResolver pathResolver,
    IBlobContentStorageProviderFactory providerFactory,
    IPublishingService publishingService)
{
    public const string ContentType = "themes";
    public const string DefaultTheme = "default";

    public virtual async Task<ThemeStorage> OpenAsync(string storeId)
    {
        var store = await storeService.GetNoCloneAsync(storeId, StoreResponseGroup.DynamicProperties.ToString());
        var theme = store?.DynamicProperties?.FirstOrDefault(x => x.Name == "DefaultThemeName")?.Values?.FirstOrDefault()?.Value?.ToString();
        theme = string.IsNullOrEmpty(theme) ? DefaultTheme : theme;

        var provider = providerFactory.CreateProvider(pathResolver.GetContentBasePath(ContentType, storeId, theme));
        return new ThemeStorage(provider, publishingService, theme);
    }
}
