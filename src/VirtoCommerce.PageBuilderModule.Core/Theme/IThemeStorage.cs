namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>Files of a store's active theme, by their path inside the theme folder.</summary>
public interface IThemeStorage
{
    string ThemeName { get; }

    /// <summary>Gets the path in the store's theme storage, as the designer's saves name it.</summary>
    string GetPath(string file);

    string GetDraftPath(string file);

    string GetLivePath(string draftFile);

    /// <summary>Returns null when the file does not exist.</summary>
    Task<string> ReadAsync(string file);

    Task WriteAsync(string file, string content);

    /// <summary>Returns null when the file does not exist or does not hold a <typeparamref name="T"/>.</summary>
    Task<T> ReadJsonAsync<T>(string file) where T : class;

    Task WriteJsonAsync<T>(string file, T value) where T : class;

    Task RemoveAsync(IEnumerable<string> files);

    Task<IList<string>> ListFilesAsync(string folder);

    Task<IList<string>> ListFoldersAsync(string folder);

    /// <summary>Falls back to the latest complete publication when the pointer cannot be read. Returns null when there is none.</summary>
    Task<int?> GetCurrentPublicationAsync();

    Task SetCurrentPublicationAsync(int publication);

    Task<int> GetNextPublicationAsync();

    /// <summary>Returns null for an incomplete publication: its record is written last.</summary>
    Task<ThemePublicationRecord> GetPublicationRecordAsync(int publication);

    Task WritePublicationRecordAsync(int publication, ThemePublicationRecord record);

    Task<string> ReadSnapshotAsync(int publication, string language);

    Task WriteSnapshotAsync(int publication, string language, string content);
}
