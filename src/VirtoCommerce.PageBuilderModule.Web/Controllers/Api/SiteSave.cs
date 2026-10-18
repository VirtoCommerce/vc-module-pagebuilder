using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    /// <summary>
    /// The designer action a request is part of: the designer sends one id with every request of a Save (or Restore,
    /// or Discard), and each change of the version history it records carries it, so the history shows them as one.
    /// </summary>
    public static class SiteSave
    {
        public const string Header = "X-PageBuilder-Save";

        // it ends up in a commit message: an id, nothing that could read as another line of it
        private static readonly Regex Id = new("^[A-Za-z0-9-]{8,64}$", RegexOptions.None, TimeSpan.FromSeconds(1));
        private static readonly Regex ShortSha = new("^[0-9a-f]{7,40}$", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>The request's save id; <c>null</c> when it sends none, or something that isn't one.</summary>
        public static string Of(HttpRequest request)
        {
            var value = request?.Headers[Header].ToString();
            return !string.IsNullOrEmpty(value) && Id.IsMatch(value) ? value : null;
        }

        /// <summary>
        /// How a restore names the version it brought back: the one the editor picked (a Save restored as a whole names
        /// one version for all its files), else the restored commit.
        /// </summary>
        public static string Version(string version, string sha) =>
            !string.IsNullOrEmpty(version) && ShortSha.IsMatch(version) ? version[..7] : sha[..7];
    }
}
