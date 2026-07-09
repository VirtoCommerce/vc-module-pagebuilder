using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Where a page lives in the content repository, and on which branch an editor's draft of it lives.
    /// </summary>
    public static class GitPageLocation
    {
        private const int MaxSlugLength = 60;
        private const int HashLength = 7;

        private static readonly Regex Unsafe = new("[^a-z0-9._-]+", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Repository path of a page: the blob path under the configured pages root.
        /// </summary>
        public static string RepoPath(string pagesRoot, string pagePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pagePath);

            return $"{(pagesRoot ?? string.Empty).Trim('/')}/{Normalize(pagePath)}";
        }

        /// <summary>
        /// The work branch for one editor editing one page. It is cut from the production branch at the
        /// first save and deleted once the page is published, so it only ever holds that page.
        /// <para>
        /// The page path is flattened into the slug rather than nested: git will not let
        /// <c>designer/john/industry</c> and <c>designer/john/industry/manufacturing</c> both exist, as
        /// a ref cannot be a file and a directory at once. Flattening can collide (<c>a/b</c> and
        /// <c>a-b</c> both become <c>a-b</c>), so a short hash of the full path is appended.
        /// </para>
        /// </summary>
        public static string BranchFor(string template, string userName, string pagePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(template);

            var normalized = Normalize(pagePath);
            var user = SanitizeRefComponent(userName) ?? "unknown";

            return template
                .Replace("{user}", user, StringComparison.Ordinal)
                .Replace("{slug}", Slug(normalized), StringComparison.Ordinal);
        }

        /// <summary>
        /// A single path segment of a git ref: lowercase, no spaces or punctuation git would reject.
        /// Returns <c>null</c> when nothing usable is left — a caller decides what "unknown" means.
        /// </summary>
        public static string SanitizeRefComponent(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var sanitized = Unsafe.Replace(value.ToLowerInvariant(), "-").Trim('-', '.');
            return sanitized.Length == 0 ? null : sanitized;
        }

        private static string Slug(string normalizedPath)
        {
            var flattened = SanitizeRefComponent(normalizedPath.Replace('/', '-')) ?? "page";
            if (flattened.Length > MaxSlugLength)
            {
                flattened = flattened[..MaxSlugLength].Trim('-', '.');
            }

            return $"{flattened}-{ShortHash(normalizedPath)}";
        }

        // identity of the page, not a security boundary: it only has to separate paths that flatten alike
        private static string ShortHash(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexStringLower(hash)[..HashLength];
        }

        private static string Normalize(string pagePath) => pagePath.Replace('\\', '/').TrimStart('/');
    }
}
