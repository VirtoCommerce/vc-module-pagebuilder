using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// The trailer this module writes into its commit messages to record who did the edit in platform
    /// terms.
    /// <para>
    /// A commit carries a git name and email and no login, and a person's platform login and their git
    /// address are usually different — on the content repository every commit the module made is signed
    /// with one shared fallback address, so git attribution alone cannot answer "who did this". The
    /// trailer is written by the server from the authenticated identity and never from a request, which
    /// is what makes it the authoritative answer while git authorship stays cosmetic.
    /// </para>
    /// </summary>
    public static class GitCommitMessage
    {
        public const string VcUserTrailer = "VC-User";

        /// <summary>
        /// The commit message with the platform login recorded under it. Returns the message untouched
        /// when there is no login to record — an unauthenticated write has nothing to attribute, and a
        /// trailer with an empty value would only look like one.
        /// </summary>
        public static string WithVcUser(string message, string vcUser) =>
            string.IsNullOrWhiteSpace(vcUser) ? message : $"{message}\n\n{VcUserTrailer}: {vcUser.Trim()}";

        /// <summary>
        /// Reads a trailer out of a commit message body, or <c>null</c> when it carries none. Commits
        /// made outside the module have no trailer, and that is an answer rather than a problem.
        /// </summary>
        public static string ReadTrailer(string messageBody, string name)
        {
            if (string.IsNullOrEmpty(messageBody) || string.IsNullOrEmpty(name))
            {
                return null;
            }

            var prefix = name + ":";

            // Last one wins: a rebase or an amend can leave an older trailer above the current one.
            string value = null;
            foreach (var line in messageBody.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var candidate = trimmed[prefix.Length..].Trim();
                    if (candidate.Length > 0)
                    {
                        value = candidate;
                    }
                }
            }

            return value;
        }
    }
}
