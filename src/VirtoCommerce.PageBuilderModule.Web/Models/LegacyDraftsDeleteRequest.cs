using System.Collections.Generic;

namespace VirtoCommerce.PageBuilderModule.Web.Models
{
    /// <summary>
    /// The blob draft files a cleanup run is asked to remove, named one by one.
    /// <para>
    /// Explicit paths rather than "delete everything that matches": the caller has read the inventory and
    /// decided which of those files are dead weight. A server-side sweep would also take the drafts that
    /// appeared between the inventory and this call — which are precisely the ones nobody has looked at.
    /// </para>
    /// </summary>
    public class LegacyDraftsDeleteRequest
    {
        public IList<string> Paths { get; set; } = new List<string>();

        /// <summary>
        /// Report what would happen and write nothing.
        /// <para>
        /// Defaults to <c>true</c> so that an omitted field is the harmless one. This endpoint deletes
        /// content that has no other copy; a caller that forgets the flag should get a report, not a
        /// cleanup it did not ask for.
        /// </para>
        /// </summary>
        public bool DryRun { get; set; } = true;
    }
}
