using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemeStorage(IBlobContentStorageProvider provider, IPublishingService publishingService, string themeName) : IThemeStorage
{
    public const string TemplatesFolder = "templates";
    public const string TemplateRulesFile = "config/template_rules.json";
    public const string HeaderGroupFile = "sections/header-group.json";
    public const string FooterGroupFile = "sections/footer-group.json";
    public const string BrandFile = "config/brand.json";
    public const string SettingsDataFile = "config/settings_data.json";
    public const string PaletteFile = "config/palette.json";
    public const string DarkPaletteFile = "config/palette.dark.json";
    public const string SnapshotsFolder = "snapshots";
    public const string CurrentPublicationFile = SnapshotsFolder + "/current.json";
    public const string PublicationRecordFileName = "publication.json";
    public const string CatalogsFolder = "catalogs";
    public const string CurrentCatalogFile = CatalogsFolder + "/current.json";

    private const string FolderType = "folder";

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
    };

    public string ThemeName { get; } = themeName;

    /// <summary>Also taken by theme file saves.</summary>
    public static string GetLockResource(string storeId) => $"PageBuilder:Theme:{storeId}";

    public virtual string GetPath(string file) => $"{ThemeName}/{file}";

    public virtual string GetDraftPath(string file) => publishingService.GetRelativeDraftUrl(file, true);

    public virtual string GetLivePath(string draftFile) => publishingService.GetRelativeDraftUrl(draftFile, false);

    public virtual async Task<bool> ExistsAsync(string file)
    {
        return await provider.GetBlobInfoAsync(GetPath(file)) != null;
    }

    public virtual async Task<string> ReadAsync(string file)
    {
        if (!await ExistsAsync(file))
        {
            return null;
        }

        await using var stream = await provider.OpenReadAsync(GetPath(file));
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }

    public virtual async Task WriteAsync(string file, string content)
    {
        await using var stream = await provider.OpenWriteAsync(GetPath(file));
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }

    public virtual async Task<T> ReadJsonAsync<T>(string file) where T : class
    {
        return DeserializeJson<T>(await ReadAsync(file));
    }

    public virtual Task WriteJsonAsync<T>(string file, T value) where T : class
    {
        return WriteAsync(file, JsonConvert.SerializeObject(value, SerializerSettings));
    }

    public virtual Task RemoveAsync(IEnumerable<string> files)
    {
        return provider.RemoveAsync(files.Select(GetPath).ToArray());
    }

    public virtual async Task<IList<string>> ListFilesAsync(string folder)
    {
        var result = await provider.SearchAsync(GetPath(folder), null);

        return result.Results.Where(x => x.Type != FolderType).Select(x => x.Name).ToList();
    }

    public virtual async Task<IList<string>> ListFoldersAsync(string folder)
    {
        var result = await provider.SearchAsync(GetPath(folder), null);

        return result.Results.Where(x => x.Type == FolderType).Select(x => x.Name).ToList();
    }

    public virtual async Task<int?> GetCurrentPublicationAsync()
    {
        var pointer = await ReadAsync(CurrentPublicationFile);
        if (pointer == null)
        {
            return null;
        }

        if (GetPublication(DeserializeJson<ThemePublicationPointer>(pointer)) is { } publication)
        {
            return publication;
        }

        foreach (var candidate in (await GetPublicationsAsync()).OrderDescending())
        {
            if (await GetPublicationRecordAsync(candidate) != null)
            {
                return candidate;
            }
        }

        return null;
    }

    public virtual Task SetCurrentPublicationAsync(int publication)
    {
        var pointer = AbstractTypeFactory<ThemePublicationPointer>.TryCreateInstance();
        pointer.Publication = publication;

        return WriteJsonAsync(CurrentPublicationFile, pointer);
    }

    public virtual async Task<int> GetNextPublicationAsync()
    {
        var publications = await GetPublicationsAsync();
        var current = GetPublication(await ReadJsonAsync<ThemePublicationPointer>(CurrentPublicationFile)) ?? 0;

        return publications.Append(current).Max() + 1;
    }

    public virtual async Task<ThemePublicationRecord> GetPublicationRecordAsync(int publication)
    {
        var record = await ReadJsonAsync<ThemePublicationRecord>(GetPublicationRecordPath(publication));

        return string.IsNullOrEmpty(record?.DefaultLanguage) ? null : record;
    }

    public virtual Task WritePublicationRecordAsync(int publication, ThemePublicationRecord record)
    {
        return WriteJsonAsync(GetPublicationRecordPath(publication), record);
    }

    public virtual Task<string> ReadSnapshotAsync(int publication, string language)
    {
        return ReadAsync(GetSnapshotPath(publication, language));
    }

    public virtual Task WriteSnapshotAsync(int publication, string language, string content)
    {
        return WriteAsync(GetSnapshotPath(publication, language), content);
    }

    protected virtual string GetPublicationFolder(int publication) =>
        $"{SnapshotsFolder}/{publication.ToString(CultureInfo.InvariantCulture)}";

    protected virtual string GetSnapshotPath(int publication, string language) => $"{GetPublicationFolder(publication)}/{language}.json";

    protected virtual string GetPublicationRecordPath(int publication) => $"{GetPublicationFolder(publication)}/{PublicationRecordFileName}";

    protected virtual async Task<IList<int>> GetPublicationsAsync()
    {
        var folders = await ListFoldersAsync(SnapshotsFolder);

        return folders
            .Select(x => int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var publication) ? publication : 0)
            .Where(x => x > 0)
            .ToList();
    }

    /// <summary>Returns null when the content is missing or does not hold a <typeparamref name="T"/>.</summary>
    protected virtual T DeserializeJson<T>(string content) where T : class
    {
        if (content == null)
        {
            return null;
        }

        try
        {
            var result = AbstractTypeFactory<T>.TryCreateInstance();
            JsonConvert.PopulateObject(content, result, SerializerSettings);

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? GetPublication(ThemePublicationPointer pointer) => pointer?.Publication > 0 ? pointer.Publication : null;
}
