using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PageBuilderModule.Core;

namespace VirtoCommerce.PageBuilderModule.Data.Authorization
{
    /// <summary>Does not derive from <c>PermissionAuthorizationRequirement</c>: the platform's default handler grants those on a global permission alone, without the store check.</summary>
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
