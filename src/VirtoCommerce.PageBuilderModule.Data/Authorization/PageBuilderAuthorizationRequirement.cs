using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PageBuilderModule.Core;

namespace VirtoCommerce.PageBuilderModule.Data.Authorization
{
    /// <summary>
    /// The permission in the store of the resource: granted everywhere, or for that store through
    /// <see cref="VirtoCommerce.StoreModule.Core.Security.StoreSelectedScope"/>. Not a platform
    /// <c>PermissionAuthorizationRequirement</c>: the platform's default handler grants those on a global permission alone,
    /// without the store check.
    /// </summary>
    public sealed class PageBuilderAuthorizationRequirement : IAuthorizationRequirement
    {
        /// <summary>Reading the store's Page Builder content: the permission the store scope is assigned to.</summary>
        public PageBuilderAuthorizationRequirement()
            : this(ModuleConstants.Security.Permissions.Read)
        {
        }

        public PageBuilderAuthorizationRequirement(string permission)
        {
            Permission = permission;
        }

        public string Permission { get; }
    }
}
