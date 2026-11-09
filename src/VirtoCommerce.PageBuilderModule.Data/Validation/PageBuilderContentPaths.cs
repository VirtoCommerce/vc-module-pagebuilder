using System.Buffers;
using System.Text.RegularExpressions;
using VirtoCommerce.ContentModule.Core;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Theme;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public static class PageBuilderContentPaths
{
    private static readonly HashSet<string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ContentConstants.ContentTypes.Themes,
        ContentConstants.ContentTypes.Pages,
        ContentConstants.ContentTypes.Blogs,
    };

    // Drafts too: the admin blade opens the designer on a draft's name.
    private static readonly string[] PageExtensions = [".page", ".page-draft"];
    private const string JsonExtension = ".json";

    // The config folder ships with the theme; the builder saves only the files it owns there.
    private const string ThemeConfigFolder = "config";

    private static readonly HashSet<string> BuilderConfigFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ThemeStorage.SettingsDataFile,
        ThemeStorage.TemplateRulesFile,
        ThemeStorage.BrandFile,
        ThemeStorage.PaletteFile,
        ThemeStorage.DarkPaletteFile,
    };

    // What file storage reads as a path or a pattern rather than as part of a name, on Windows and Linux alike.
    private static readonly SearchValues<char> KeywordSeparators = SearchValues.Create("\\/:*?\"<>|");

    private static readonly Regex HistoryCursorPattern = new("^[A-Za-z0-9+/=_-]{1,256}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex ContentHistoryCursorPattern = new("^[A-Za-z0-9_-]{1,4096}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    // the catalog's file name in the theme, as the storefront names it
    private static readonly Regex CatalogHashPattern = new("^[A-Za-z0-9_-]{8,128}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    // base64url of a commit sha's 20 bytes
    private static readonly Regex HistoryVersionIdPattern = new("^[A-Za-z0-9_-]{27}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    // the id goes into a commit trailer
    private static readonly Regex SaveIdPattern = new("^[A-Za-z0-9-]{8,64}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    public static bool IsCatalogHash(string hash) => !string.IsNullOrEmpty(hash) && CatalogHashPattern.IsMatch(hash);

    public static bool IsHistoryVersionId(string versionId) => !string.IsNullOrEmpty(versionId) && HistoryVersionIdPattern.IsMatch(versionId);

    public static bool IsSaveId(string saveId) => !string.IsNullOrEmpty(saveId) && SaveIdPattern.IsMatch(saveId);

    public static bool IsContentType(string type) => type != null && ContentTypes.Contains(type);

    public static bool IsPageContentType(string type) => GitPageLocation.IsPageContent(type);

    public static bool HasPathCharactersOnly(string path) =>
        !path.Any(character => character is '\\' or ':' || char.IsControl(character));

    /// <summary>A path from the root passes: the admin blade names pages that way.</summary>
    public static bool StaysInsideTheFolder(string path)
    {
        var trimmed = path.TrimStart('/');

        return trimmed.Length > 0 && !trimmed.Split('/').Any(segment => segment is "" or "." or "..");
    }

    /// <summary>Windows file storage reads a name with a trailing dot or space as another name.</summary>
    public static bool HasPortableNames(string path) =>
        !path.Split('/').Any(segment => segment.EndsWith('.') || segment.EndsWith(' '));

    public static bool IsRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && HasPathCharactersOnly(path) && StaysInsideTheFolder(path) && HasPortableNames(path);

    public static bool IsThemeFilePath(string path) =>
        IsRelativePath(path) && !path.StartsWith('/') && IsInsideAThemeFolder(path);

    public static bool IsHistoryThemeFile(string path) =>
        IsThemeFilePath(path) && !IsInFolder(GetPathInTheme(path), ThemeStorage.SnapshotsFolder);

    public static bool IsFolderName(string name) =>
        IsRelativePath(name) && !name.Contains('/');

    public static bool IsPageDocument(string path) =>
        PageExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    public static bool IsJsonFile(string path) => path.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase);

    public static bool IsInsideAThemeFolder(string path) => path.TrimStart('/').Contains('/');

    public static bool IsWritableThemeFile(string path) => IsJsonFile(path) && IsInsideAThemeFolder(path) && CanSaveThemeFile(path);

    public static bool CanSaveThemeFile(string path)
    {
        var pathInTheme = GetPathInTheme(path);

        return !IsInFolder(pathInTheme, ThemeStorage.SnapshotsFolder) &&
               !IsInFolder(pathInTheme, ThemeStorage.CatalogsFolder) &&
               (!IsInFolder(pathInTheme, ThemeConfigFolder) || BuilderConfigFiles.Contains(pathInTheme));
    }

    public static string GetThemeName(string path)
    {
        var relativePath = (path ?? string.Empty).TrimStart('/');
        var separator = relativePath.IndexOf('/');

        return separator < 0 ? string.Empty : relativePath[..separator];
    }

    public static string GetPathInTheme(string path)
    {
        var relativePath = (path ?? string.Empty).TrimStart('/');

        return relativePath[(relativePath.IndexOf('/') + 1)..];
    }

    public static bool IsInFolder(string path, string folder) =>
        path != null && path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

    public static bool IsLegacyDraft(string path) => path.EndsWith(ModuleConstants.DraftSuffix, StringComparison.OrdinalIgnoreCase);

    public static bool IsSchemaKind(string kind) => ModuleConstants.SchemaKinds.All.Contains(kind);

    public static bool IsSearchKeyword(string keyword) =>
        keyword.IndexOfAny(KeywordSeparators) < 0 && !keyword.Any(char.IsControl);

    public static bool IsHistoryCursor(string cursor) => HistoryCursorPattern.IsMatch(cursor);

    public static bool IsContentHistoryCursor(string cursor) => ContentHistoryCursorPattern.IsMatch(cursor);
}
