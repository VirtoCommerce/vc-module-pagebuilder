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

    // ".page-draft": the admin blade opens the designer on a draft's name.
    private static readonly Dictionary<string, string[]> WritableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [ContentConstants.ContentTypes.Themes] = [".json"],
        [ContentConstants.ContentTypes.Pages] = [".page", ".page-draft"],
        [ContentConstants.ContentTypes.Blogs] = [".page", ".page-draft"],
    };

    // The config folder holds what the designer runs in its user's session: it ships with the theme, not through the builder.
    private const string ThemeConfigFolder = "config";
    private const string ThemeSettingsFile = "settings_data.json";

    public static bool IsContentType(string type) => type != null && ContentTypes.Contains(type);

    /// <summary>
    /// A leading "/" passes: the admin blade names pages that way. A segment ending in "." or " " does not:
    /// Windows file storage reads it as another name.
    /// </summary>
    public static bool IsRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl))
        {
            return false;
        }

        var trimmed = path.TrimStart('/');

        return trimmed.Length > 0 && !trimmed.Split('/').Any(segment =>
            segment is "" or "." or ".." || segment.EndsWith('.') || segment.EndsWith(' '));
    }

    public static bool IsFolderName(string name) =>
        IsRelativePath(name) && !name.Contains('/');

    /// <summary>Expects a type and a path that already passed their own rules.</summary>
    public static bool IsWritable(string type, string path)
    {
        if (!WritableExtensions.TryGetValue(type, out var extensions) ||
            !extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !string.Equals(type, ContentConstants.ContentTypes.Themes, StringComparison.OrdinalIgnoreCase) ||
               IsWritableThemeFile(path);
    }

    private static bool IsWritableThemeFile(string path)
    {
        var relativePath = path.TrimStart('/');
        var themeFolderEnd = relativePath.IndexOf('/');
        if (themeFolderEnd < 0)
        {
            return false;
        }

        var pathInTheme = relativePath[(themeFolderEnd + 1)..];

        return !pathInTheme.StartsWith(ThemeConfigFolder + "/", StringComparison.OrdinalIgnoreCase) ||
               pathInTheme.Equals($"{ThemeConfigFolder}/{ThemeSettingsFile}", StringComparison.OrdinalIgnoreCase);
    }
}
