using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    /// <summary>
    /// The shape of what a caller may name in a <see cref="PageBuilderController"/> request. Every value
    /// here ends up in a storage path, so anything that could leave the store's content folder is refused
    /// before a provider is created.
    /// </summary>
    internal static class PageBuilderRequestValidator
    {
        public const string Themes = "themes";
        public const string Pages = "pages";
        public const string Blogs = "blogs";

        private static readonly HashSet<string> ContentTypes = new(StringComparer.OrdinalIgnoreCase) { Themes, Pages, Blogs };

        // What the designer and the admin blade write: pages and blog articles are .page documents, a
        // theme's settings are JSON. A page is also named by its draft, "foo.page-draft": the admin blade
        // opens the designer on that name, and both the blob and the git flow resolve it to the page.
        private static readonly Dictionary<string, string[]> WritableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            [Themes] = [".json"],
            [Pages] = [".page", ".page-draft"],
            [Blogs] = [".page", ".page-draft"],
        };

        public static bool IsContentType(string type) => type != null && ContentTypes.Contains(type);

        /// <summary>
        /// A path inside the content folder. A leading "/" is how the admin blade names a page and is
        /// trimmed by every reader, so it is accepted; "\", "..", "." and ":" are not.
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

            return trimmed.Length > 0 && !trimmed.Split('/').Any(segment => segment is "" or "." or "..");
        }

        /// <summary>A theme is one folder name.</summary>
        public static bool IsThemeName(string theme) =>
            !string.IsNullOrWhiteSpace(theme) && IsRelativePath(theme) && !theme.Contains('/');

        public static bool IsWritableFile(string type, string path) =>
            IsContentType(type) &&
            IsRelativePath(path) &&
            WritableExtensions[type].Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}
