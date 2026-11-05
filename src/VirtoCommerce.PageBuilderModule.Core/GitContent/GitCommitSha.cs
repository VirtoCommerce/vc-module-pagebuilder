using System.Text.RegularExpressions;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Tells a commit sha from any other ref. A sha is immutable, which is what makes it the only thing
    /// worth addressing a version by: a branch name means something different tomorrow.
    /// <para>
    /// Also a guard on input. A ref taken from a request ends up in a redirect to the storefront and in a
    /// restore that reads content out of the repository, so accepting "any ref" would let a client choose
    /// what those two point at.
    /// </para>
    /// </summary>
    public static class GitCommitSha
    {
        private static readonly Regex Pattern = new("^[0-9a-f]{40}$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

        private static readonly Regex AbbreviatedPattern = new("^[0-9a-f]{7,40}$", RegexOptions.None, TimeSpan.FromSeconds(1));

        public static bool IsSha(string value) => !string.IsNullOrEmpty(value) && Pattern.IsMatch(value);

        /// <summary>A sha as git shortens it: its first characters, seven at least.</summary>
        public static bool IsAbbreviatedSha(string value) => !string.IsNullOrEmpty(value) && AbbreviatedPattern.IsMatch(value);
    }
}
