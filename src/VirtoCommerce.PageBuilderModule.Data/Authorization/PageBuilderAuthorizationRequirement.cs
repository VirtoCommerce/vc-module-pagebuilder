using Microsoft.AspNetCore.Authorization;

namespace VirtoCommerce.PageBuilderModule.Data.Authorization
{
    public sealed class PageBuilderAuthorizationRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// A back-office manager usually has no store on the account; the action's permission still guards them.
        /// </summary>
        public bool AllowUsersWithoutStore { get; init; }
    }
}
