using System.Buffers;
using VirtoCommerce.ContentModule.Core;

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

    // The config folder holds what the designer runs in its user's session: it ships with the theme, not through the builder.
    private const string ThemeConfigFolder = "config";
    private const string ThemeSettingsFile = "settings_data.json";

    // What file storage reads as a path or a pattern rather than as part of a name, on Windows and Linux alike.
    private static readonly SearchValues<char> KeywordSeparators = SearchValues.Create("\\/:*?\"<>|");

    public static bool IsContentType(string type) => type != null && ContentTypes.Contains(type);

    public static bool IsPageContentType(string type) =>
        string.Equals(type, ContentConstants.ContentTypes.Pages, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, ContentConstants.ContentTypes.Blogs, StringComparison.OrdinalIgnoreCase);

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

    public static bool IsFolderName(string name) =>
        IsRelativePath(name) && !name.Contains('/');

    public static bool IsPageDocument(string path) =>
        PageExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    public static bool IsJsonFile(string path) => path.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase);

    public static bool IsInsideAThemeFolder(string path) => path.TrimStart('/').Contains('/');

    /// <summary>No theme folder check: the file rules check that first.</summary>
    public static bool IsOutsideTheThemeConfig(string path)
    {
        var relativePath = path.TrimStart('/');
        var pathInTheme = relativePath[(relativePath.IndexOf('/') + 1)..];

        return !pathInTheme.StartsWith(ThemeConfigFolder + "/", StringComparison.OrdinalIgnoreCase) ||
               pathInTheme.Equals($"{ThemeConfigFolder}/{ThemeSettingsFile}", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSearchKeyword(string keyword) =>
        keyword.IndexOfAny(KeywordSeparators) < 0 && !keyword.Any(char.IsControl);
}
