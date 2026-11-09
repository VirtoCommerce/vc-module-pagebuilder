using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.Platform.Security.Authorization;

namespace VirtoCommerce.PageBuilderModule.Data.Authorization
{
    /// <summary>
    /// The permission in the store of the resource: granted everywhere, or for that store through
    /// <see cref="VirtoCommerce.StoreModule.Core.Security.StoreSelectedScope"/>.
    /// </summary>
    public sealed class PageBuilderAuthorizationRequirement : PermissionAuthorizationRequirement
    {
        /// <summary>Reading the store's Page Builder content: the permission the store scope is assigned to.</summary>
        public PageBuilderAuthorizationRequirement()
            : this(ModuleConstants.Security.Permissions.Read)
        {
        }

        public PageBuilderAuthorizationRequirement(string permission)
            : base(permission)
        {
        }
    }
}
