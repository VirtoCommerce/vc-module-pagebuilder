namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// The GitHub App a site's "Enable history" setup created in the customer's organization: what GitHub
    /// handed over once the App was made, kept by the platform in place of configuration values.
    /// </summary>
    public sealed class GitHubAppConnection
    {
        /// <summary>The organization the App belongs to, and the one that holds the site repositories.</summary>
        public string Organization { get; init; }

        public string AppId { get; init; }

        /// <summary>The App's url name: "github.com/apps/{slug}".</summary>
        public string AppSlug { get; init; }

        /// <summary>The App's private key in PEM form. Stored only protected with the platform's keys.</summary>
        public string PrivateKey { get; init; }
    }
}
