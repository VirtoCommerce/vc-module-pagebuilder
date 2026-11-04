using VirtoCommerce.ContentModule.Core;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

/// <summary>
/// The rules the builder validators are made of. Every value they check ends up in a storage path, so
/// anything that could leave the store's content folder is refused.
/// </summary>
public static class PageBuilderContentPaths
{
    private static readonly HashSet<string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ContentConstants.ContentTypes.Themes,
        ContentConstants.ContentTypes.Pages,
        ContentConstants.ContentTypes.Blogs,
    };

    // What the designer and the admin blade write: pages and blog articles are .page documents, a
    // theme's settings are JSON. A page is also named by its draft, "foo.page-draft": the admin blade
    // opens the designer on that name, and both the blob and the git flow resolve it to the page.
    private static readonly Dictionary<string, string[]> WritableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [ContentConstants.ContentTypes.Themes] = [".json"],
        [ContentConstants.ContentTypes.Pages] = [".page", ".page-draft"],
        [ContentConstants.ContentTypes.Blogs] = [".page", ".page-draft"],
    };

    // A theme's config folder is the designer's own configuration: request descriptors
    // (builder_settings.json), section and template schemas with their expressions, the settings
    // schema. The designer runs it in the session of whoever opens it, so it is deployed with the
    // theme, never written through the builder. The one file the designer saves there is the theme
    // settings.
    private const string ThemeConfigFolder = "config";
    private const string ThemeSettingsFile = "settings_data.json";

    public static bool IsContentType(string type) => type != null && ContentTypes.Contains(type);

    /// <summary>
    /// A path inside the content folder. A leading "/" is how the admin blade names a page and is
    /// trimmed by every reader, so it is accepted; "\", "..", "." and ":" are not, and neither is a
    /// segment ending in a dot or a space, which Windows file storage would read as another name.
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

    /// <summary>One folder name, such as a theme.</summary>
    public static bool IsFolderName(string name) =>
        IsRelativePath(name) && !name.Contains('/');

    /// <summary>
    /// A file the designer writes. The content type and the path are checked on their own; this is
    /// only what a valid path of a valid type may name.
    /// </summary>
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

    /// <summary>
    /// A theme file outside the theme's config folder — "default/content/templates/product.json", say —
    /// or the theme settings, "default/config/settings_data.json". It is always inside a theme folder.
    /// </summary>
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
